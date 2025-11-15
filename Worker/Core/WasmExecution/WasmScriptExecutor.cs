using System.Text;
using Wasmtime;
using Worker.Core.HostApi;

namespace Worker.Core.WasmExecution;

/// <summary>
/// Executes JavaScript code within a QuickJS-in-WASM sandbox.
/// Manages WASM module lifecycle, memory operations, and error handling.
/// </summary>
public class WasmScriptExecutor : IWasmScriptExecutor
{
    private readonly IHostApi _hostApi;

    public WasmScriptExecutor(IHostApi? hostApi = null)
    {
        _hostApi = hostApi ?? new HostApiImpl();
    }

    /// <inheritdoc />
    public void ExecuteScript(string jsCode)
    {
        if (string.IsNullOrEmpty(jsCode))
        {
            throw new ArgumentException("JavaScript code cannot be null or empty.", nameof(jsCode));
        }

        using var engine = new Wasmtime.Engine();
        var wasmPath = ResolveWasmPath();

        if (!File.Exists(wasmPath))
        {
            throw new FileNotFoundException(
                $"WASM module not found at {wasmPath}. Please build the QuickJS WASM module first.");
        }

        using var module = Module.FromFile(engine, wasmPath);
        using var linker = new Linker(engine);
        using var store = new Store(engine);

        ConfigureWasi(store);
        DefineHostBridge(store, linker);

        var instance = linker.Instantiate(store, module);
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
    /// Loads the assistantApi bootstrap JavaScript from disk.
    /// This JS is prepended to all user scripts to provide the high-level API.
    /// </summary>
    /// <returns>The bootstrap JavaScript code as a string.</returns>
    private static string LoadBootstrapJs()
    {
        var candidates = new[]
        {
            // Relative to output directory
            Path.Combine(AppContext.BaseDirectory, "assistantApi.js"),
            Path.Combine(AppContext.BaseDirectory, "scripts", "assistantApi.js"),
            
            // Relative to repo root (development)
            Path.Combine(AppContext.BaseDirectory, "..", "..", "scripts", "assistantApi.js"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "scripts", "assistantApi.js"),
        };

        foreach (var path in candidates)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }
        }

        throw new FileNotFoundException(
            "assistantApi.js bootstrap file not found. Searched locations:\n" +
            string.Join("\n", candidates.Select(Path.GetFullPath)));
    }

    /// <summary>
    /// Resolves the path to the WASM module by checking multiple candidates.
    /// Useful for supporting various deployment and development scenarios.
    /// </summary>
    private static string ResolveWasmPath()
    {
        var candidates = new[]
        {
            // Relative to output directory
            Path.Combine(AppContext.BaseDirectory, "assistant.wasm"),
            Path.Combine(AppContext.BaseDirectory, "wasm", "assistant.wasm"),
            
            // Relative to repo root (development)
            Path.Combine(AppContext.BaseDirectory, "..", "..", "wasm", "assistant.wasm"),
            
            // Add platform-specific paths if needed
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wasm", "assistant.wasm"),
        };

        foreach (var path in candidates)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // Return default for helpful error messaging
        return Path.Combine(AppContext.BaseDirectory, "assistant.wasm");
    }

    /// <summary>
    /// Dispatches a host method call from the sandbox and returns the JSON response.
    /// </summary>
    private string HandleHostCall(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var method = doc.RootElement.GetProperty("method").GetString();
            var args = doc.RootElement.GetProperty("args");

            return method switch
            {
                "Log" => HandleLogCall(args),
                "Add" => HandleAddCall(args),
                "Subtract" => HandleSubtractCall(args),
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
    private string HandleLogCall(System.Text.Json.JsonElement args)
    {
        var message = args[0].GetString();
        _hostApi.Log(message!);
        return "{\"result\":null}";
    }

    /// <summary>
    /// Handles the Add host call from the sandbox.
    /// </summary>
    private string HandleAddCall(System.Text.Json.JsonElement args)
    {
        var a = args[0].GetInt32();
        var b = args[1].GetInt32();
        var sum = _hostApi.Add(a, b);
        return $"{{\"result\":{sum}}}";
    }

    /// <summary>
    /// Handles the Subtract host call from the sandbox.
    /// </summary>
    private string HandleSubtractCall(System.Text.Json.JsonElement args)
    {
        var a = args[0].GetInt32();
        var b = args[1].GetInt32();
        var difference = _hostApi.Subtract(a, b);
        return $"{{\"result\":{difference}}}";
    }
}
