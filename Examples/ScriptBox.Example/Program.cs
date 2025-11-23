using System.Collections.Generic;
using ScriptBox.Demo;
using ScriptBox;
using ScriptBox.Core.Configuration;

Console.WriteLine("=== ScriptBox Demo ===");

var sandboxDir = Path.Combine(Environment.CurrentDirectory, "sandbox");
Directory.CreateDirectory(sandboxDir);

var sandboxConfig = new SandboxConfiguration
{
    SandboxDirectory = sandboxDir,
    BootstrapScripts = new List<string>()
};

var scriptBox = ScriptBoxBuilder
    .Create()
    .WithSandboxConfiguration(sandboxConfig)
    .RegisterApisFrom(typeof(DemoCalculatorApi))
    .WithExecutionTimeout(TimeSpan.FromSeconds(5))
    .Build();

var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "sample-script.js");
var script = File.ReadAllText(scriptPath);

await using (scriptBox)
await using (var session = scriptBox.CreateSession())
{
    await session.RunAsync(script);
}

Console.WriteLine("See sandbox/demo/result.txt for output.");
