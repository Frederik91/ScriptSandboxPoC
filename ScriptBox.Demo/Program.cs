using System.Text.Json;
using ScriptBox.Net.Core.Configuration;
using ScriptBox.Net.Core.HostApi;
using ScriptBox.Net.Core.WasmExecution;
using ScriptBox.Net.Services;

Console.WriteLine("=== ScriptBox Demo ===");

var sandboxDir = Path.Combine(Environment.CurrentDirectory, "sandbox");
Directory.CreateDirectory(sandboxDir);

var config = new SandboxConfiguration
{
    SandboxDirectory = sandboxDir,
    BootstrapScripts = new List<string>
    {
        Path.Combine("scripts", "scriptbox.js"),
        Path.Combine("scripts", "demo-api.js")
    }
};

var hostApi = new HostApiImpl(config);
var executor = new WasmScriptExecutor(hostApi, config);
var worker = new WorkerMethods(executor);

var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "sample-script.js");
var script = File.ReadAllText(scriptPath);
worker.RunScript(script);

Console.WriteLine("See sandbox/demo/result.txt for output.");
