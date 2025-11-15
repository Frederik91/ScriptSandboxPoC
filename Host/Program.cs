// 1. Start Worker process (dotnet Worker.dll)
using System.Diagnostics;
using System.IO.Pipes;
using StreamJsonRpc;

// 1. Build worker if needed
var workerDir = GetWorkerDirectory();
var workerDll = Path.Combine(workerDir, "bin", "Debug", "net9.0", "Worker.dll");

// 2. Create named pipes for bidirectional communication
var pipeName = $"ScriptSandbox_{Random.Shared.NextInt64():X16}";
using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

// 3. Start Worker process with pipe name
var psi = new ProcessStartInfo
{
    FileName = "dotnet",
    Arguments = $"\"{workerDll}\" {pipeName}",
    RedirectStandardError = true,
    UseShellExecute = false,
};

var worker = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start worker.");

// Log any errors from worker to help debug
if (worker.StandardError != null)
{
    _ = Task.Run(async () =>
    {
        string? line;
        while ((line = await worker.StandardError.ReadLineAsync()) != null)
        {
            Console.Error.WriteLine($"[Worker] {line}");
        }
    });
}

// 4. Wait for worker to connect to the pipe
await pipe.WaitForConnectionAsync();

// 5. Set up JSON-RPC over the pipe
var hostApi = new HostApi();

var rpc = new JsonRpc(pipe, pipe, hostApi);

rpc.StartListening();

// 3. Example JS code (this would later be AI-generated TS compiled to JS)
var repoRoot = GetRepoRoot();
var jsCode = File.ReadAllText(Path.Combine(repoRoot, "scripts", "dist", "sample-script.js"));

Console.WriteLine("Sending script to worker...");
await rpc.InvokeAsync<object>("Worker.RunScript", jsCode);
Console.WriteLine("Script finished.");
rpc.Dispose();

// Clean up
await rpc.Completion;
worker.WaitForExit();

static string GetWorkerDirectory()
{
    string? currentDir = GetRepoRoot();

    return Path.Combine(currentDir, "Worker");
}

static string GetRepoRoot()
{

    var currentDir = AppContext.BaseDirectory;
    // Assumes Worker project is in ../Worker relative to Host project
    while (!string.IsNullOrEmpty(currentDir) && !Directory.Exists(Path.Combine(currentDir, "Worker")))
    {
        currentDir = Path.GetDirectoryName(currentDir);
    }

    if (string.IsNullOrEmpty(currentDir))
        throw new DirectoryNotFoundException("Could not find Worker directory.");

    return currentDir;
}

public class HostApi
{
    [JsonRpcMethod("Host.Log")]
    public Task LogAsync(string message)
    {
        Console.WriteLine("[script] " + message);
        return Task.CompletedTask;
    }

    [JsonRpcMethod("Host.Add")]
    public Task<int> AddAsync(int a, int b)
    {
        var sum = a + b;
        Console.WriteLine($"[host] Add({a}, {b}) = {sum}");
        return Task.FromResult(sum);
    }
}