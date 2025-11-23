using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Wasmtime;
using ScriptBox.Core.Configuration;
using ScriptBox.Core.HostApi;
using ScriptBox.Core.Runtime;

namespace ScriptBox.Core.WasmExecution;

/// <summary>
/// Executes JavaScript code within a QuickJS-in-WASM sandbox.
/// Manages WASM module lifecycle, memory operations, and error handling.
/// </summary>
#if NET6_0_OR_GREATER
internal sealed class WasmScriptExecutor : IWasmScriptExecutor, IAsyncDisposable
{
#else
internal sealed class WasmScriptExecutor : IWasmScriptExecutor, IDisposable
{
#endif
    private readonly IHostApi _hostApi;
    private readonly SandboxConfiguration _config;
    private readonly Dictionary<string, Func<HostCallContext, Task<object?>>> _jsonHandlers;
    private readonly Engine _engine;
    private readonly Module _module;
    private readonly JsonSerializerOptions _jsonOptions;
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
        _jsonHandlers = new Dictionary<string, Func<HostCallContext, Task<object?>>>(StringComparer.OrdinalIgnoreCase);
        if (jsonHandlers != null)
        {
            foreach (var kvp in jsonHandlers)
            {
                _jsonHandlers[kvp.Key] = kvp.Value;
            }
        }
        _moduleSource = moduleSource ?? WasmModuleSource.FromBytes(DefaultRuntimeResources.LoadEmbeddedWasm());
        _engine = new Engine();
        _module = _moduleSource.CreateModule(_engine);
        
        // Initialize JSON serializer options with appropriate settings for the target framework
#if NET6_0_OR_GREATER
        _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
#else
        _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
#endif
    }

    public WasmScriptExecutor(SandboxConfiguration? config)
        : this(null, config, null, null)
    {
    }

    /// <inheritdoc />
    public string ExecuteScript(string jsCode, int? timeoutMs = null)
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
                return task.Result;
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
            return ExecuteScriptInternal(jsCode);
        }
    }

    /// <summary>
    /// Internal method that performs the actual script execution without timeout handling.
    /// </summary>
    private string ExecuteScriptInternal(string jsCode)
    {
        using var linker = new Linker(_engine);
        using var store = new Store(_engine);

        ConfigureWasi(store);
        DefineHostBridge(store, linker);

        var instance = linker.Instantiate(store, _module);
        var memory = instance.GetMemory(WasmConfiguration.MemoryExportName)
                    ?? throw new InvalidOperationException($"No {WasmConfiguration.MemoryExportName} export found");

        // Prepend bootstrap JS before user code
        // Add void expression to discard bootstrap result, then evaluate user code
        var startupJs = LoadStartupJs();
        string fullScript;
        if (string.IsNullOrWhiteSpace(startupJs))
        {
            fullScript = WrapUserScriptInIife(jsCode);
        }
        else
        {
            // Terminate bootstrap, add void 0 to discard any bootstrap return value,
            // then wrap user code in an IIFE to support return statements at the top level
            fullScript = $"{startupJs};\nvoid 0;\n{WrapUserScriptInIife(jsCode)}";
        }

        return ExecuteEvalFunction(instance, memory, fullScript);
    }

    /// <summary>
    /// Wraps user script code in an Immediately Invoked Function Expression (IIFE).
    /// This allows scripts to use top-level return statements, which aligns with
    /// how AI models typically generate JavaScript code.
    /// </summary>
    /// <param name="jsCode">The user script code to wrap</param>
    /// <returns>The user code wrapped in an IIFE that is immediately invoked</returns>
    private static string WrapUserScriptInIife(string jsCode)
    {
        return $"(function() {{\n{jsCode}\n}})()";
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

        var message = ReadStringFromMemory(memory, ptr, len);
        _hostApi.Log(message);
    }

    /// <summary>
    /// Callback invoked when QuickJS calls a host method.
    /// Reads the request, dispatches it, and writes the response back to WASM memory.
    /// </summary>
    private int HandleHostCallCallback(Caller caller, int inPtr, int inLen, int outPtr, int outCap)
    {
        try
        {
            var memory = caller.GetMemory(WasmConfiguration.MemoryExportName)
                        ?? throw new InvalidOperationException("No memory export");

            var jsonRequest = ReadStringFromMemory(memory, inPtr, inLen);
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
        catch (Exception)
        {
            // System.Console.Error.WriteLine($"[HostCall Error] {ex}");
            throw; // Re-throw to cause trap
        }
    }

    /// <summary>
    /// Executes the eval_js WASM function and checks for errors.
    /// </summary>
    private string ExecuteEvalFunction(Instance instance, Memory memory, string jsCode)
    {
        var (ptr, len) = WriteStringToMemory(instance, memory, jsCode);

        var eval = instance.GetFunction<int, int, int>(WasmConfiguration.EvalFunctionName)
                  ?? throw new InvalidOperationException(
                      $"{WasmConfiguration.EvalFunctionName} function not found");

        var status = eval(ptr, len);

        if (status != WasmConfiguration.SuccessStatusCode)
        {
            var errorMessage = ReadErrorMessage(instance, memory);
            System.Console.Error.WriteLine($"WASM eval_js status={status}: {errorMessage}");
            throw new InvalidOperationException(
                $"eval_js failed with status {status}. Error: {errorMessage}");
        }

        // Success - read the result
        return ReadResultMessage(instance, memory);
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

        return ReadStringFromMemory(memory, errorPtr, errorLen);
    }

    /// <summary>
    /// Reads the result value from WASM memory after successful evaluation.
    /// </summary>
    private string ReadResultMessage(Instance instance, Memory memory)
    {
        var getResultPtr = instance.GetFunction<int>(WasmConfiguration.GetResultPtrFunctionName)
                          ?? throw new InvalidOperationException(
                              $"{WasmConfiguration.GetResultPtrFunctionName} function not found");
        var getResultLen = instance.GetFunction<int>(WasmConfiguration.GetResultLenFunctionName)
                          ?? throw new InvalidOperationException(
                              $"{WasmConfiguration.GetResultLenFunctionName} function not found");

        int resultPtr = getResultPtr();
        int resultLen = getResultLen();

        if (resultLen <= 0)
        {
            return string.Empty;
        }

        return ReadStringFromMemory(memory, resultPtr, resultLen);
    }

    /// <summary>
    /// Writes JavaScript source code into WASM linear memory.
    /// </summary>
    /// <returns>A tuple of (memory offset, byte length).</returns>
    private static (int ptr, int len) WriteStringToMemory(Instance instance, Memory memory, string jsCode)
    {
        var bytes = Encoding.UTF8.GetBytes(jsCode);
        var (scriptPtr, maxScriptSize) = GetScriptBufferLocation(instance);

        if (bytes.Length > maxScriptSize)
        {
            throw new InvalidOperationException(
                $"Script too large ({bytes.Length} bytes) for available WASM memory " +
                $"(max {maxScriptSize} bytes)");
        }

        for (var i = 0; i < bytes.Length; i++)
        {
            memory.Write(scriptPtr + i, bytes[i]);
        }

        return (scriptPtr, bytes.Length);
    }

    /// <summary>
    /// Determines the location and size of the script buffer in WASM memory.
    /// Prefers dynamic lookup via exported functions, falls back to hardcoded defaults.
    /// </summary>
    private static (int ptr, int len) GetScriptBufferLocation(Instance instance)
    {
        // Try to get the dynamic script buffer from the WASM module
        var getScriptBufferPtr = instance.GetFunction<int>(WasmConfiguration.GetScriptBufferPtrFunctionName);
        var getScriptBufferLen = instance.GetFunction<int>(WasmConfiguration.GetScriptBufferLenFunctionName);

        if (getScriptBufferPtr != null && getScriptBufferLen != null)
        {
            return (getScriptBufferPtr(), getScriptBufferLen());
        }

        // Fallback to hardcoded offset for backward compatibility with older WASM modules
        return (WasmConfiguration.ScriptMemoryOffset, 
                WasmConfiguration.MaxScriptSize - WasmConfiguration.ScriptMemoryOffset);
    }

    private static string ReadStringFromMemory(Memory memory, int ptr, int length)
    {
#if NETSTANDARD2_0
        var buffer = new byte[length];
        for (var i = 0; i < length; i++)
        {
            buffer[i] = memory.Read<byte>(ptr + i);
        }
        return Encoding.UTF8.GetString(buffer);
#else
        if (length == 0)
        {
            return string.Empty;
        }

        Span<byte> buffer = length <= 1024 ? stackalloc byte[length] : new byte[length];
        for (var i = 0; i < length; i++)
        {
            buffer[i] = memory.Read<byte>(ptr + i);
        }

        return Encoding.UTF8.GetString(buffer);
#endif
    }

    /// <summary>
    /// Loads configured bootstrap JavaScript files from disk.
    /// These are prepended before every user script.
    /// </summary>
    /// <returns>The bootstrap JavaScript code as a string.</returns>
    private string LoadStartupJs()
    {
        var scripts = _config.StartupScripts ?? new List<string>();
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

            if (_jsonHandlers.Count > 0 && _jsonHandlers.TryGetValue(method!, out var handler))
            {
                var context = HostCallContext.FromJson(method!, root, CancellationToken.None);
                var result = handler(context).GetAwaiter().GetResult();
                var response = JsonSerializer.Serialize(new { result }, _jsonOptions);
                return response;
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
                
                // Tool Invocation Protocol
                "tool.invoke" => HandleToolInvoke(args),

                _ => $"{{\"error\":\"Unknown method: {method}\"}}"
            };
        }
        catch (Exception ex)
        {
            return $"{{\"error\":\"Error processing host call: {ex.Message}\"}}";
        }
    }

    /// <summary>
    /// Handles the tool.invoke host call from the sandbox.
    /// This is used by the bootstrap-utils.ts proxy to invoke tools dynamically.
    /// </summary>
    private string HandleToolInvoke(JsonElement args)
    {
        // args[0] is the JSON string of the request
        var requestJson = args[0].GetString();
        if (string.IsNullOrEmpty(requestJson))
        {
             return "{\"error\":\"Missing request JSON\"}";
        }

        using var doc = JsonDocument.Parse(requestJson);
        var root = doc.RootElement;
        
        if (!root.TryGetProperty("toolId", out var toolIdElement))
        {
            return "{\"error\":\"Missing toolId\"}";
        }
        
        var toolId = toolIdElement.GetString();
        if (string.IsNullOrEmpty(toolId))
        {
            return "{\"error\":\"Empty toolId\"}";
        }

        if (_jsonHandlers.TryGetValue(toolId!, out var handler))
        {
             // HostCallContext.FromJson expects an object with "args" property, 
             // which matches the ToolInvocationRequest structure.
             var context = HostCallContext.FromJson(toolId!, root, CancellationToken.None);
             var result = handler(context).GetAwaiter().GetResult();
             return JsonSerializer.Serialize(new { result }, _jsonOptions);
        }
        
        return $"{{\"error\":\"Unknown tool: {toolId}\"}}";
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
#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return default(ValueTask);
        }

        _module.Dispose();
        _engine.Dispose();
        _disposed = true;
        return default(ValueTask);
    }
#else
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _module.Dispose();
        _engine.Dispose();
        _disposed = true;
    }
#endif
}
