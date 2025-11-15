// Set up JSON-RPC over pipes passed as arguments
using StreamJsonRpc;
using System.IO.Pipes;
using Microsoft.Win32.SafeHandles;
using Wasmtime;
using System.Text;

if (args.Length < 1)
{
        System.Console.Error.WriteLine("Worker expects one argument: pipeName");
        Environment.Exit(1);
}

var pipeName = args[0];

try
{
        // Connect to the named pipe created by the host
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync();

        var rpc = new JsonRpc(pipe, pipe);
        var rpcAdapter = new JsonRpcClientAdapter(rpc);

        var workerMethods = new WorkerMethods(rpcAdapter);

        rpc.AddLocalRpcTarget(workerMethods);
        rpc.StartListening();

        // Wait until the host closes the stream
        await rpc.Completion;
}
catch (Exception ex)
{
        System.Console.Error.WriteLine($"Worker error: {ex}");
        throw;
}


public interface IRpcClient
{
        Task<T> InvokeAsync<T>(string method, params object?[]? arguments);
}

public class JsonRpcClientAdapter : IRpcClient
{
        private readonly JsonRpc _jsonRpc;

        public JsonRpcClientAdapter(JsonRpc jsonRpc)
        {
                _jsonRpc = jsonRpc;
        }

        public async Task<T> InvokeAsync<T>(string method, params object?[]? arguments)
        {
                return await _jsonRpc.InvokeAsync<T>(method, arguments);
        }
}

public class WorkerMethods
{
        private readonly IRpcClient _rpc;

        public WorkerMethods(IRpcClient rpc)
        {
                _rpc = rpc;
        }

        [JsonRpcMethod("Worker.RunScript")]
        public void RunScript(string jsCode)
        {
                using var engine = new Wasmtime.Engine();
                var wasmPath = ResolveWasmPath();
                if (!File.Exists(wasmPath))
                {
                        throw new FileNotFoundException($"WASM module not found at {wasmPath}. Please build the QuickJS WASM module first.");
                }
                using var module = Module.FromFile(engine, wasmPath);
                using var linker = new Linker(engine);
                using var store = new Store(engine);
                store.SetWasiConfiguration(
                new WasiConfiguration()
                        .WithArgs("guest-app-name")
                        .WithInheritedStandardOutput()
                        .WithInheritedStandardError()
                );


                // 1. Define host imports that QuickJS expects (wasi, env, etc.)
                //    Often needed if your QuickJS build is WASI-based.
                linker.DefineWasi();

                // 2. Define the generic host.call bridge
                //    JSON-RPC goes through this single function
                linker.Define(
                    "host",
                    "call",
                    Function.FromCallback(
                        store,
                        (Caller caller, int inPtr, int inLen, int outPtr, int outCap) =>
                        {
                            var memory = caller.GetMemory("memory")
                                ?? throw new InvalidOperationException("No memory export");

                            // Read input JSON from WASM memory
                            Span<byte> inBuf = stackalloc byte[inLen];
                            for (var i = 0; i < inLen; i++)
                            {
                                inBuf[i] = memory.Read<byte>(inPtr + i);
                            }

                            var jsonIn = Encoding.UTF8.GetString(inBuf);

                            // Dispatch to C# and get JSON response
                            var jsonOut = HandleHostCall(jsonIn);
                            var outBytes = Encoding.UTF8.GetBytes(jsonOut);

                            // Write output JSON to WASM memory
                            int bytesToWrite = Math.Min(outBytes.Length, outCap);
                            for (var i = 0; i < bytesToWrite; i++)
                            {
                                memory.Write(outPtr + i, outBytes[i]);
                            }

                            return bytesToWrite;
                        }
                    )
                );

                // 3. Instantiate QuickJS
                var instance = linker.Instantiate(store, module);
                var memory = instance.GetMemory("memory")
                               ?? throw new InvalidOperationException("No memory export");

                var eval = instance.GetFunction<int, int, int>("eval_js")
                           ?? throw new InvalidOperationException("eval_js function not found");

                // 4. Write jsCode into WASM memory and call eval_js
                var (ptr, len) = WriteStringToMemory(memory, jsCode);
                var status = eval(ptr, len);
                
                // Read error message regardless of status
                var getErrorPtr = instance.GetFunction<int>("get_last_error_ptr")
                                 ?? throw new InvalidOperationException("get_last_error_ptr function not found");
                var getErrorLen = instance.GetFunction<int>("get_last_error_len")
                                 ?? throw new InvalidOperationException("get_last_error_len function not found");
                
                int errorPtr = getErrorPtr();
                int errorLen = getErrorLen();
                
                string errorMsg = "";
                if (errorLen > 0)
                {
                    Span<byte> errorBuf = stackalloc byte[errorLen];
                    for (int i = 0; i < errorLen; i++)
                    {
                        errorBuf[i] = memory.Read<byte>(errorPtr + i);
                    }
                    errorMsg = Encoding.UTF8.GetString(errorBuf);
                    System.Console.Error.WriteLine($"WASM eval_js status={status}: {errorMsg}");
                }
                
                if (status != 0)
                {
                        throw new InvalidOperationException($"eval_js failed with status {status}. Error: {errorMsg}");
                }

                // 5. (Optional) cleanup / finalize VM
        }

        private static (int ptr, int len) WriteStringToMemory(Memory memory, string jsCode)
        {
                var bytes = Encoding.UTF8.GetBytes(jsCode);
                // Start right after the QuickJS runtime structures (typically < 1KB)
                // Use a smaller offset for smaller WASM modules
                const int ptr = 0x2000; // 8KB offset - safer default
                const int maxMemory = 0x100000; // 1MB max for safety

                if (ptr + bytes.Length > maxMemory)
                {
                        throw new InvalidOperationException($"Script too large ({bytes.Length} bytes) for available WASM memory");
                }

                for (var i = 0; i < bytes.Length; i++)
                {
                        memory.Write(ptr + i, bytes[i]); // symmetric with Read<T>(long)
                }

                return (ptr, bytes.Length);
        }

        private string HandleHostCall(string json)
        {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var method = doc.RootElement.GetProperty("method").GetString();
                var args = doc.RootElement.GetProperty("args");

                switch (method)
                {
                        case "Log":
                                var message = args[0].GetString();
                                _rpc.InvokeAsync<object?>("Host.Log", message!).GetAwaiter().GetResult();
                                return "{\"result\":null}";

                        case "Add":
                                var a = args[0].GetInt32();
                                var b = args[1].GetInt32();
                                var sum = _rpc.InvokeAsync<int>("Host.Add", a, b).GetAwaiter().GetResult();
                                return $"{{\"result\":{sum}}}";

                        default:
                                return "{\"error\":\"Unknown method\"}";
                }
        }

        /// <summary>
        /// Resolves the path to the custom-built WASM module.
        /// Tries multiple locations: current directory, repo root wasm/, and relative paths.
        /// </summary>
        private static string ResolveWasmPath()
        {
                // Try relative to output directory
                var candidates = new[]
                {
                        Path.Combine(AppContext.BaseDirectory, "assistant.wasm"),
                        Path.Combine(AppContext.BaseDirectory, "wasm", "assistant.wasm"),
                        // Try relative to repo root (for development)
                        Path.Combine(AppContext.BaseDirectory, "..", "..", "wasm", "assistant.wasm"),
                        // Absolute from common build location
                        "/Users/frederik/repos/IsolatedTypeScript/wasm/assistant.wasm",
                };

                foreach (var path in candidates)
                {
                        var fullPath = Path.GetFullPath(path);
                        if (File.Exists(fullPath))
                        {
                                return fullPath;
                        }
                }

                // If not found, return the default location with helpful error message
                return Path.Combine(AppContext.BaseDirectory, "assistant.wasm");
        }
}

public class AssistantApi
{
        private readonly IRpcClient _rpc;

        public AssistantApi(IRpcClient rpc)
        {
                _rpc = rpc;
        }

        // JS: await assistantApi.log("message");
        public async Task log(string message)
        {
                await _rpc.InvokeAsync<object?>("Host.Log", message);
        }

        // JS: const sum = await assistantApi.add(2, 3);
        public async Task<int> add(int a, int b)
        {
                return await _rpc.InvokeAsync<int>("Host.Add", a, b);
        }
}