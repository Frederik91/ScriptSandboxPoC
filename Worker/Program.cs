// Set up JSON-RPC over pipes passed as arguments
using Jint;
using StreamJsonRpc;
using System.IO.Pipes;
using Microsoft.Win32.SafeHandles;

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

    var workerMethods = new WorkerMethods(rpc);

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


public class WorkerMethods
{
    private readonly JsonRpc _rpc;

    public WorkerMethods(JsonRpc rpc)
    {
        _rpc = rpc;
    }

    // Host will call "Worker.RunScript" with the JS source
    [JsonRpcMethod("Worker.RunScript")]
    public void RunScript(string jsCode)
    {
        var engine = new Engine();

        // Inject assistantApi into JS runtime
        engine.SetValue("assistantApi", new AssistantApi(_rpc));

        // Execute the script. Convention: script calls run() itself or does everything top-level.
        engine.Execute(jsCode);
    }
}

public class AssistantApi
{
    private readonly JsonRpc _rpc;

    public AssistantApi(JsonRpc rpc)
    {
        _rpc = rpc;
    }

    // Called from JS: assistantApi.log("message")
    public void log(string message)
    {
        // Synchronous bridge into host JSON-RPC
        _rpc.InvokeAsync<object>("Host.Log", message).GetAwaiter().GetResult();
    }

    // Called from JS: var s = assistantApi.add(2, 3);
    public int add(int a, int b)
    {
        return _rpc.InvokeAsync<int>("Host.Add", a, b).GetAwaiter().GetResult();
    }
}