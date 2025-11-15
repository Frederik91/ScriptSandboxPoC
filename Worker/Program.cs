using StreamJsonRpc;
using System.IO.Pipes;
using Worker.Core.RpcClient;
using Worker.Core.WasmExecution;
using Worker.Services;

const string LogPrefix = "[Worker]";

try
{
    ValidateArgs(args);
    var pipeName = args[0];

    // Connect to the host via named pipe
    using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    await pipe.ConnectAsync();

    // Set up RPC
    var rpc = new JsonRpc(pipe, pipe);
    var rpcAdapter = new JsonRpcClientAdapter(rpc);

    // Set up script execution
    var scriptExecutor = new WasmScriptExecutor(rpcAdapter);
    var workerMethods = new WorkerMethods(scriptExecutor);

    // Register RPC methods and start listening
    rpc.AddLocalRpcTarget(workerMethods);
    rpc.StartListening();

    // Wait until the host closes the connection
    await rpc.Completion;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogPrefix} Fatal error: {ex}");
    Environment.Exit(1);
}

static void ValidateArgs(string[] args)
{
    if (args.Length < 1)
    {
        Console.Error.WriteLine($"[Worker] Error: Worker expects one argument: pipeName");
        Environment.Exit(1);
    }
}