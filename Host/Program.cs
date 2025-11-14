// 1. Start Worker process (dotnet Worker.dll)
using System.Diagnostics;
using StreamJsonRpc;

var psi = new ProcessStartInfo
{
    FileName = "dotnet",
    Arguments = "Worker.dll",
    WorkingDirectory = GetWorkerDirectory(),
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    UseShellExecute = false,
};

var worker = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start worker.");

// 2. Set up JSON-RPC over worker's stdin/stdout
var hostApi = new HostApi();

var rpc = new JsonRpc(
    worker.StandardOutput.BaseStream,
    worker.StandardInput.BaseStream,
    hostApi);

rpc.StartListening();

// 3. Example JS code (this would later be AI-generated TS compiled to JS)
var jsCode = @"
function run() {
  assistantApi.log('Hello from JS script');
  var sum = assistantApi.add(2, 3);
  assistantApi.log('2 + 3 = ' + sum);
}
run();
";

Console.WriteLine("Sending script to worker...");
await rpc.InvokeAsync<object>("Worker.RunScript", jsCode);
Console.WriteLine("Script finished.");

// Clean up
worker.StandardInput.Close();
await rpc.Completion;
worker.WaitForExit();

static string GetWorkerDirectory()
{
    var currentDir = AppContext.BaseDirectory;
    // Assumes Worker project is in ../Worker relative to Host project
    return Path.Combine(currentDir, "../../../../Worker/Worker.csproj");
}

public class HostApi
{
    // Exposed as "Host.Log" to the worker
    [JsonRpcMethod("Host.Log")]
    public void Log(string message)
    {
        Console.WriteLine("[script] " + message);
    }

    // Exposed as "Host.Add" to the worker
    [JsonRpcMethod("Host.Add")]
    public int Add(int a, int b)
    {
        var sum = a + b;
        Console.WriteLine($"[host] Add({a}, {b}) = {sum}");
        return sum;
    }
}