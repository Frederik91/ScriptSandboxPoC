using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Wasmtime;
using ScriptBox.Net.Core.Configuration;
using ScriptBox.Net.Core.HostApi;
using ScriptBox.Net.Core.Runtime;

namespace ScriptBox.Net.Core.WasmExecution;

/// <summary>
/// Executes JavaScript code within a QuickJS-in-WASM sandbox.
/// Manages WASM module lifecycle, memory operations, and error handling.
/// </summary>
public class WasmScriptExecutor : IWasmScriptExecutor, IAsyncDisposable
{
    private readonly IHostApi _hostApi;
    private readonly SandboxConfiguration _config;
    private readonly IReadOnlyDictionary<string, Func<HostCallContext, Task<object?>>> _jsonHandlers;
    private readonly Engine _engine;
    private readonly Module _module;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly WasmModuleSource _moduleSource;
    private bool _disposed;

    public WasmScriptExecutor(
        IHostApi? hostApi = null,
        SandboxConfiguration? config = null,
        IReadOnlyDictionary<string, Func<HostCallContext, Task<object?>>>? jsonHandlers = null,
        WasmModuleSource? moduleSource = null)
    {
        _config = config ?? SandboxConfiguration.CreateDefault();
        _hostApi = hostApi ?? new HostApiImpl(_config);
        _jsonHandlers = jsonHandlers != null
            ? new Dictionary<string, Func<HostCallContext, Task<object?>>>(jsonHandlers, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, Func<HostCallContext, Task<object?>>>(StringComparer.OrdinalIgnoreCase);
        _moduleSource = moduleSource ?? WasmModuleSource.FromBytes(DefaultRuntimeResources.LoadEmbeddedWasm());
        _engine = new Engine();
        _module = _moduleSource.CreateModule(_engine);
    }

    public WasmScriptExecutor(SandboxConfiguration? config)
        : this(null, config, null, null)
    {
    }

    /// <inheritdoc />
    public void ExecuteScript(string jsCode, int? timeoutMs = null)
    {
        if (string.IsNullOrEmpty(jsCode))
        {
            throw new ArgumentException("JavaScript code cannot be null or empty.", nameof(jsCode));
        }

        var effectiveTimeout = timeoutMs ?? WasmConfiguration.DefaultTimeoutMs;

        if (effectiveTimeout > 0)
        {
            // Use Task-based timeout for compatible timeout handling
            var task = Task.Run(() => ExecuteScriptInternal(jsCode));

            try
            {
                if (!task.Wait(effectiveTimeout))
                {
                    throw new TimeoutException(
                        $"Script execution exceeded timeout limit of {effectiveTimeout}ms. " +
                        "The script may have an infinite loop or is taking too long to complete.");
                }
            }
            catch (AggregateException ae)
            {
                // Unwrap AggregateException from Task.Wait
                throw ae.InnerException ?? ae;
            }
        }
        else
        {
            // No timeout - execute directly
            ExecuteScriptInternal(jsCode);
        }
    }

    /// <summary>
    /// Internal method that performs the actual script execution without timeout handling.
    /// </summary>
    private void ExecuteScriptInternal(string jsCode)
    {
        using var linker = new Linker(_engine);
        using var store = new Store(_engine);

        ConfigureWasi(store);
        DefineHostBridge(store, linker);

        var instance = linker.Instantiate(store, _module);
        var memory = instance.GetMemory(WasmConfiguration.MemoryExportName)
                    ?? throw new InvalidOperationException($"No {WasmConfiguration.MemoryExportName} export found");

        // Prepend bootstrap JS before user code
        var bootstrapJs = LoadBootstrapJs();
        var fullScript = bootstrapJs + "\n" + jsCode;

        ExecuteEvalFunction(instance, memory, fullScript);
    }

    /// <summary>
    /// Configures WASI for the sandbox environment.
    /// </summary>
    private static void ConfigureWasi(Store store)
    {
        store.SetWasiConfiguration(
            new WasiConfiguration()
                .WithArgs("guest-app-name")
                .WithInheritedStandardOutput()
                .WithInheritedStandardError()
        );
    }

    /// <summary>
    /// Defines the host.call bridge that allows QuickJS to invoke host methods.
    /// </summary>
    private void DefineHostBridge(Store store, Linker linker)
    {
        linker.DefineWasi();
        linker.Define(
            "host",
            "call",
            Function.FromCallback(
                store,
                (Caller caller, int inPtr, int inLen, int outPtr, int outCap) =>
                    HandleHostCallCallback(caller, inPtr, inLen, outPtr, outCap)
            )
        );

        // host.log: used by console.log inside QuickJS
        linker.Define(
            "host",
            "log",
            Function.FromCallback(
                store,
                (Caller caller, int ptr, int len) =>
                    HandleHostLogCallback(caller, ptr, len)
            )
        );
    }

    private void HandleHostLogCallback(Caller caller, int ptr, int len)
    {
        var memory = caller.GetMemory(WasmConfiguration.MemoryExportName)
                    ?? throw new InvalidOperationException("No memory export");

        Span<byte> buf = stackalloc byte[len];
        for (var i = 0; i < len; i++)
        {
            buf[i] = memory.Read<byte>(ptr + i);
        }

        var message = Encoding.UTF8.GetString(buf);
        _hostApi.Log(message);
    }

    /// <summary>
    /// Callback invoked when QuickJS calls a host method.
    /// Reads the request, dispatches it, and writes the response back to WASM memory.
    /// </summary>
    private int HandleHostCallCallback(Caller caller, int inPtr, int inLen, int outPtr, int outCap)
    {
        var memory = caller.GetMemory(WasmConfiguration.MemoryExportName)
                    ?? throw new InvalidOperationException("No memory export");

        // Read request JSON from WASM memory
        Span<byte> inBuf = stackalloc byte[inLen];
        for (var i = 0; i < inLen; i++)
        {
            inBuf[i] = memory.Read<byte>(inPtr + i);
        }

        var jsonRequest = Encoding.UTF8.GetString(inBuf);
        var jsonResponse = HandleHostCall(jsonRequest);
        var responseBytes = Encoding.UTF8.GetBytes(jsonResponse);

        // Write response JSON to WASM memory
        int bytesToWrite = Math.Min(responseBytes.Length, outCap);
        for (var i = 0; i < bytesToWrite; i++)
        {
            memory.Write(outPtr + i, responseBytes[i]);
        }

        return bytesToWrite;
    }

    /// <summary>
    /// Executes the eval_js WASM function and checks for errors.
    /// </summary>
    private void ExecuteEvalFunction(Instance instance, Memory memory, string jsCode)
    {
        var (ptr, len) = WriteStringToMemory(memory, jsCode);

        var eval = instance.GetFunction<int, int, int>(WasmConfiguration.EvalFunctionName)
                  ?? throw new InvalidOperationException(
                      $"{WasmConfiguration.EvalFunctionName} function not found");

        var status = eval(ptr, len);
        var errorMessage = ReadErrorMessage(instance, memory);

        if (status != WasmConfiguration.SuccessStatusCode)
        {
            System.Console.Error.WriteLine($"WASM eval_js status={status}: {errorMessage}");
            throw new InvalidOperationException(
                $"eval_js failed with status {status}. Error: {errorMessage}");
        }
    }

    /// <summary>
    /// Reads the error message from WASM memory after evaluation.
    /// </summary>
    private string ReadErrorMessage(Instance instance, Memory memory)
    {
        var getErrorPtr = instance.GetFunction<int>(WasmConfiguration.GetErrorPtrFunctionName)
                         ?? throw new InvalidOperationException(
                             $"{WasmConfiguration.GetErrorPtrFunctionName} function not found");
        var getErrorLen = instance.GetFunction<int>(WasmConfiguration.GetErrorLenFunctionName)
                         ?? throw new InvalidOperationException(
                             $"{WasmConfiguration.GetErrorLenFunctionName} function not found");

        int errorPtr = getErrorPtr();
        int errorLen = getErrorLen();

        if (errorLen <= 0)
        {
            return string.Empty;
        }

        Span<byte> errorBuf = stackalloc byte[errorLen];
        for (int i = 0; i < errorLen; i++)
        {
            errorBuf[i] = memory.Read<byte>(errorPtr + i);
        }

        return Encoding.UTF8.GetString(errorBuf);
    }

    /// <summary>
    /// Writes JavaScript source code into WASM linear memory.
    /// </summary>
    /// <returns>A tuple of (memory offset, byte length).</returns>
    private static (int ptr, int len) WriteStringToMemory(Memory memory, string jsCode)
    {
        var bytes = Encoding.UTF8.GetBytes(jsCode);

        if (WasmConfiguration.ScriptMemoryOffset + bytes.Length > WasmConfiguration.MaxScriptSize)
        {
            throw new InvalidOperationException(
                $"Script too large ({bytes.Length} bytes) for available WASM memory " +
                $"(max {WasmConfiguration.MaxScriptSize - WasmConfiguration.ScriptMemoryOffset} bytes)");
        }

        for (var i = 0; i < bytes.Length; i++)
        {
            memory.Write(WasmConfiguration.ScriptMemoryOffset + i, bytes[i]);
        }

        return (WasmConfiguration.ScriptMemoryOffset, bytes.Length);
    }

    /// <summary>
    /// Loads configured bootstrap JavaScript files from disk.
    /// These are prepended before every user script.
    /// </summary>
    /// <returns>The bootstrap JavaScript code as a string.</returns>
    private string LoadBootstrapJs()
    {
        var scripts = _config.BootstrapScripts ?? new List<string>();
        if (scripts.Count == 0)
        {
            return string.Empty;
        }

        return BootstrapScriptLoader.LoadScripts(scripts);
    }

    /// <summary>
    /// Dispatches a host method call from the sandbox and returns the JSON response.
    /// </summary>
    private string HandleHostCall(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("method", out var methodElement))
            {
                return "{\"error\":\"Host call missing method\"}";
            }

            var method = methodElement.GetString();
            if (string.IsNullOrWhiteSpace(method))
            {
                return "{\"error\":\"Host call missing method\"}";
            }

            if (_jsonHandlers.Count > 0 && _jsonHandlers.TryGetValue(method, out var handler))
            {
                var context = HostCallContext.FromJson(method, root, CancellationToken.None);
                var result = handler(context).GetAwaiter().GetResult();
                return JsonSerializer.Serialize(new { result }, _jsonOptions);
            }

            if (!root.TryGetProperty("args", out var args))
            {
                return $"{{\"error\":\"Host call '{method}' missing args array\"}}";
            }

            return method switch
            {
                "Log" => HandleLogCall(args),
                "Add" => HandleAddCall(args),
                "Subtract" => HandleSubtractCall(args),

                // File System API
                "FileSystemReadFile" => HandleFileSystemReadFileCall(args),
                "FileSystemWriteFile" => HandleFileSystemWriteFileCall(args),
                "FileSystemListFiles" => HandleFileSystemListFilesCall(args),
                "FileSystemExists" => HandleFileSystemExistsCall(args),
                "FileSystemDelete" => HandleFileSystemDeleteCall(args),
                "FileSystemCreateDirectory" => HandleFileSystemCreateDirectoryCall(args),

                // HTTP Client API
                "HttpGet" => HandleHttpGetCall(args),
                "HttpPost" => HandleHttpPostCall(args),
                "HttpRequest" => HandleHttpRequestCall(args),

                _ => $"{{\"error\":\"Unknown method: {method}\"}}"
            };
        }
        catch (Exception ex)
        {
            return $"{{\"error\":\"Error processing host call: {ex.Message}\"}}";
        }
    }

    /// <summary>
    /// Handles the Log host call from the sandbox.
    /// </summary>
    private string HandleLogCall(JsonElement args)
    {
        var message = args[0].GetString();
        _hostApi.Log(message!);
        return "{\"result\":null}";
    }

    /// <summary>
    /// Handles the Add host call from the sandbox.
    /// </summary>
    private string HandleAddCall(JsonElement args)
    {
        var a = args[0].GetInt32();
        var b = args[1].GetInt32();
        var sum = _hostApi.Add(a, b);
        return $"{{\"result\":{sum}}}";
    }

    /// <summary>
    /// Handles the Subtract host call from the sandbox.
    /// </summary>
    private string HandleSubtractCall(JsonElement args)
    {
        var a = args[0].GetInt32();
        var b = args[1].GetInt32();
        var difference = _hostApi.Subtract(a, b);
        return $"{{\"result\":{difference}}}";
    }

    #region File System API Handlers

    /// <summary>
    /// Handles the FileSystemReadFile host call from the sandbox.
    /// </summary>
    private string HandleFileSystemReadFileCall(JsonElement args)
    {
        var path = args[0].GetString() ?? throw new ArgumentException("path is required");
        var content = _hostApi.FileSystemReadFile(path);
        return JsonSerializer.Serialize(new { result = content });
    }

    /// <summary>
    /// Handles the FileSystemWriteFile host call from the sandbox.
    /// </summary>
    private string HandleFileSystemWriteFileCall(JsonElement args)
    {
        var path = args[0].GetString() ?? throw new ArgumentException("path is required");
        var content = args[1].GetString() ?? throw new ArgumentException("content is required");
        _hostApi.FileSystemWriteFile(path, content);
        return "{\"result\":null}";
    }

    /// <summary>
    /// Handles the FileSystemListFiles host call from the sandbox.
    /// </summary>
    private string HandleFileSystemListFilesCall(JsonElement args)
    {
        var path = args[0].GetString() ?? throw new ArgumentException("path is required");
        var filesJson = _hostApi.FileSystemListFiles(path);
        return $"{{\"result\":{filesJson}}}";
    }

    /// <summary>
    /// Handles the FileSystemExists host call from the sandbox.
    /// </summary>
    private string HandleFileSystemExistsCall(JsonElement args)
    {
        var path = args[0].GetString() ?? throw new ArgumentException("path is required");
        var exists = _hostApi.FileSystemExists(path);
        return $"{{\"result\":{exists.ToString().ToLowerInvariant()}}}";
    }

    /// <summary>
    /// Handles the FileSystemDelete host call from the sandbox.
    /// </summary>
    private string HandleFileSystemDeleteCall(JsonElement args)
    {
        var path = args[0].GetString() ?? throw new ArgumentException("path is required");
        _hostApi.FileSystemDelete(path);
        return "{\"result\":null}";
    }

    /// <summary>
    /// Handles the FileSystemCreateDirectory host call from the sandbox.
    /// </summary>
    private string HandleFileSystemCreateDirectoryCall(JsonElement args)
    {
        var path = args[0].GetString() ?? throw new ArgumentException("path is required");
        _hostApi.FileSystemCreateDirectory(path);
        return "{\"result\":null}";
    }

    #endregion

    #region HTTP Client API Handlers

    /// <summary>
    /// Handles the HttpGet host call from the sandbox.
    /// </summary>
    private string HandleHttpGetCall(JsonElement args)
    {
        var url = args[0].GetString() ?? throw new ArgumentException("url is required");
        var responseBody = _hostApi.HttpGet(url);
        return JsonSerializer.Serialize(new { result = responseBody });
    }

    /// <summary>
    /// Handles the HttpPost host call from the sandbox.
    /// </summary>
    private string HandleHttpPostCall(JsonElement args)
    {
        var url = args[0].GetString() ?? throw new ArgumentException("url is required");
        var dataJson = args[1].GetString() ?? "{}";
        var responseBody = _hostApi.HttpPost(url, dataJson);
        return JsonSerializer.Serialize(new { result = responseBody });
    }

    /// <summary>
    /// Handles the HttpRequest host call from the sandbox.
    /// </summary>
    private string HandleHttpRequestCall(JsonElement args)
    {
        var optionsJson = args[0].GetString() ?? throw new ArgumentException("options is required");
        var responseJson = _hostApi.HttpRequest(optionsJson);
        return $"{{\"result\":{responseJson}}}";
    }

    #endregion
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _module.Dispose();
        _engine.Dispose();
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
