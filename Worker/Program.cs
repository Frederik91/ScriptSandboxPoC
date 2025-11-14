// Set up JSON-RPC over stdin/stdout
using Jint;
using StreamJsonRpc;

var input = Console.OpenStandardInput();
var output = Console.OpenStandardOutput();

var rpc = new JsonRpc(input, output);
var workerMethods = new WorkerMethods(rpc);

rpc.AddLocalRpcTarget(workerMethods);
rpc.StartListening();

// Wait until the host closes the stream
await rpc.Completion;


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