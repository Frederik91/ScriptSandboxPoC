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
                var wasmPath = Path.Combine(AppContext.BaseDirectory, "qjs-wasi.wasm");
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

                // 2. Define our own host functions that JS will use via assistantApi
                //    These are the bridge to Host via JSON-RPC
                linker.Define(
                "host",
                "log",
                Function.FromCallback(
                        store,
                        (Caller caller, int ptr, int len) =>
                        {
                                var memory = caller.GetMemory("memory")
                                                ?? throw new InvalidOperationException("No memory export");

                                Span<byte> buffer = stackalloc byte[len];

                                for (var i = 0; i < len; i++)
                                {
                                        // Your API: T Memory.Read<T>(long address)
                                        buffer[i] = memory.Read<byte>(ptr + i);
                                }

                                var msg = Encoding.UTF8.GetString(buffer);
                                _rpc.InvokeAsync<object?>("Host.Log", msg).GetAwaiter().GetResult();
                        }
                )
                );

                linker.Define(
                    "host",
                    "add",
                    Function.FromCallback(store, (int a, int b) =>
                    {
                            return _rpc.InvokeAsync<int>("Host.Add", a, b).GetAwaiter().GetResult();
                    }));

                // 3. Instantiate QuickJS
                var instance = linker.Instantiate(store, module);
                var memory = instance.GetMemory("memory")
                               ?? throw new InvalidOperationException("No memory export");

                var functions = instance.GetFunctions(); // Ensure functions are loaded
                var init = instance.GetAction("_start");   // or equivalent
                var eval = instance.GetFunction<int, int, int>("qjs_eval");

                init?.Invoke();

                // 4. Write jsCode into WASM memory and call qjs_eval
                var (ptr, len) = WriteStringToMemory(memory, jsCode);
                var status = eval!(ptr, len);
                if (status != 0)
                {
                        throw new InvalidOperationException($"qjs_eval failed with status {status}.");
                }
                eval!.Invoke(ptr, len);

                // 5. (Optional) cleanup / finalize VM
        }

        private static (int ptr, int len) WriteStringToMemory(Memory memory, string jsCode)
        {
                var bytes = Encoding.UTF8.GetBytes(jsCode);
                const int ptr = 1024; // simple fixed offset for PoC

                for (var i = 0; i < bytes.Length; i++)
                {
                        memory.Write(ptr + i, bytes[i]); // symmetric with Read<T>(long)
                }

                return (ptr, bytes.Length);
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