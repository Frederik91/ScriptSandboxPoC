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
    public async Task RunScriptAsync(string jsCode)
    {
        var engine = new Engine(cfg => cfg
            .AllowClr() // optional if you ever need it
        );

        engine.SetValue("assistantApi", new AssistantApi(_rpc));

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

    // JS: await assistantApi.log("message");
    public async Task log(string message)
    {
        await _rpc.InvokeAsync<object>("Host.Log", message);
    }

    // JS: const sum = await assistantApi.add(2, 3);
    public async Task<int> add(int a, int b)
    {
        return await _rpc.InvokeAsync<int>("Host.Add", a, b);
    }
}