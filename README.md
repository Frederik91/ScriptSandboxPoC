# ScriptBox

ScriptBox is a reusable QuickJS-in-WASM sandbox for .NET. It lets you run untrusted JavaScript/TypeScript snippets inside a deterministic WASM runtime while exposing a curated host API written in C#. The runtime ships as a NuGet package and can be configured through a fluent builder; dependency injection support lives in a separate optional package.

## Performance Benchmarks

We ran a simple benchmark comparing **Standard Tool Calling** (LLM calls tools one by one) vs **ScriptBox** (LLM writes a script to call tools). The results show significant token savings for multi-step tasks.

| Task | Tool Calling Tokens | ScriptBox Tokens | Reduction |
|------|---------------------|------------------|-----------|
| **Math Operations** (7 steps) | 15,808 | 5756 | **~64%** |
| **String/Array Ops** (8 steps) | 16,860 | 7,520 | **~55%** |

*Note: These results are from our own internal benchmarks (`Examples/Scriptbox.SemanticKernel.Example`) running on GPT-4o-mini. Actual savings depend on task complexity and prompt structure.*

## Packages

| Package | Description |
| ------- | ----------- |
| `ScriptBox` | Core runtime, builder, WASM bridge, attribute-based API discovery |
| `ScriptBox.DependencyInjection` | Optional helpers that wire `ScriptBoxBuilder` into `Microsoft.Extensions.DependencyInjection` |
| `ScriptBox.SemanticKernel` | Semantic Kernel integration (plugin registration, `run_js` tool, TypeScript declarations) |

## Quick Start (without DI)

```csharp
using ScriptBox;
using ScriptBox.Core.Runtime;

[SandboxApi("calculator")]
public static class CalculatorApi
{
    [SandboxMethod("add")]
    public static Task<int> AddAsync(int a, int b) => Task.FromResult(a + b);
}

var sandbox = ScriptBoxBuilder
    .Create()
    .RegisterApisFrom(typeof(CalculatorApi)) // static API, no DI needed
    .Build();

await using var session = sandbox.CreateSession();
var result = await session.RunAsync(@"
    const sum = calculator.add(1, 2);
    sum;
");
```

### Instance APIs without DI

```csharp
[SandboxApi("files")]
public class FileApi
{
    [SandboxMethod("readText")]
    public string ReadText(string path) => File.ReadAllText(path);
}

var sandbox = ScriptBoxBuilder
    .Create()
    .RegisterApisFrom<FileApi>()             // ScriptBox will use Activator.CreateInstance<FileApi>()
    .Build();
```

## Using ScriptBox with Dependency Injection

Install both packages:

```xml
<PackageReference Include="ScriptBox" Version="*" />
<PackageReference Include="ScriptBox.DependencyInjection" Version="*" />
```

Then integrate inside `Program.cs` or wherever you build your `IServiceProvider`:

```csharp
using ScriptBox;
using ScriptBox.DependencyInjection;

[SandboxApi("calculator")]
public class CalculatorApi
{
    private readonly ILogger<CalculatorApi> _logger;
    public CalculatorApi(ILogger<CalculatorApi> logger) => _logger = logger;

    [SandboxMethod("add")]
    public int Add(int a, int b)
    {
        _logger.LogInformation("Adding {A} + {B}", a, b);
        return a + b;
    }
}

builder.Services.AddTransient<CalculatorApi>();

builder.Services.AddScriptBox((box, sp) =>
{
    box.RegisterApisFrom<CalculatorApi>();   // instance resolved via DI (ActivatorUtilities)
});
```

At runtime you can inject `IScriptBox` anywhere:

```csharp
public class ScriptRunner
{
    private readonly IScriptBox _box;
    public ScriptRunner(IScriptBox box) => _box = box;

    public Task<object?> RunAsync(string script) =>
        _box.CreateSession().RunAsync(script);
}
```

This interface makes it easier to mock `ScriptBox` in unit tests.

## Configuration & Security

You can control file system access, network access, and resource limits using the fluent builder API.

### Using Builder Methods (Recommended)

```csharp
var sandbox = ScriptBoxBuilder.Create()
    .ConfigureFileSystem(fs =>
    {
        fs.WithRootDirectory(Path.Combine(Directory.GetCurrentDirectory(), "MySafeSandbox"));
        // Optional: Add a consent hook for access outside the sandbox
        fs.WithConsentHook(context => 
        {
            Console.WriteLine($"Allow {context.Operation} on {context.Path}?");
            return Console.ReadLine() == "y";
        });
    })
    .ConfigureNetwork(network =>
    {
        network.WithAllowedDomains("api.example.com", "microsoft.com");
        network.WithRequestTimeout(TimeSpan.FromSeconds(10));
        network.WithMaxResponseSize(5 * 1024 * 1024); // 5MB
        
        // Configure the underlying HttpClient
        network.ConfigureHttpClient(client => 
        {
            client.DefaultRequestHeaders.Add("User-Agent", "ScriptBox/1.0");
        });

        // Optional: Provide a custom HttpClient factory (e.g. for mocking or auth)
        network.WithHttpClient(() => 
        {
            var handler = new HttpClientHandler();
            // handler.Proxy = ...
            return new HttpClient(handler);
        });

        // Optional: Add a consent hook for domains not in the whitelist
        network.WithConsentHook(context =>
        {
            Console.WriteLine($"Allow {context.Request.Method} to {context.Request.RequestUri}?");
            return Console.ReadLine() == "y";
        });
    })
    .Build();
```

### Using Configuration Object

```csharp
using ScriptBox.Core.Configuration;

var config = new SandboxConfiguration
{
    // File System: Restrict access to a specific directory
    SandboxDirectory = Path.Combine(Directory.GetCurrentDirectory(), "MySafeSandbox"),

    // Network: Whitelist allowed domains (empty = allow all, null = allow all)
    AllowedHttpDomains = new List<string> { "api.example.com", "microsoft.com" },

    // Limits
    MaxHttpResponseSize = 5 * 1024 * 1024, // 5MB
    HttpTimeoutMs = 10000 // 10 seconds
};

var sandbox = ScriptBoxBuilder.Create()
    .WithSandboxConfiguration(config)
    .Build();
```

## Semantic Kernel Integration
// ...existing code...
// Configure sandbox security
builder.AddScriptBox(
    configure: scriptBox =>
    {
        // Configure security directly on the builder
        scriptBox.ConfigureNetwork(net => net.WithAllowedDomains("api.weather.gov"));
        scriptBox.ConfigureFileSystem(fs => fs.WithRootDirectory("./safe-root"));

        // Register plugins to make available as js apis
        scriptBox.RegisterSemanticKernelPlugin<ClockPlugin>("time");
    }
);

var kernel = builder.Build();
// ...existing code...

## Semantic Kernel Integration

Install the additional package when you want Semantic Kernel agents to call ScriptBox through a single tool:

```xml
<PackageReference Include="ScriptBox.SemanticKernel" Version="*" />
```

The snippet below distills the approach used in `Examples/Scriptbox.SemanticKernel.Example`: register a Semantic Kernel plugin as a ScriptBox namespace, expose it through the `scriptbox.run_js` tool, and keep both sides strongly typed.

```csharp
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ScriptBox.SemanticKernel;

var builder = Kernel.CreateBuilder();

// Register your preferred chat completion connector (Azure OpenAI, OpenAI, local LLM, etc.)
// Example: builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);

// Configure sandbox security
builder.AddScriptBox(
    configure: scriptBox =>
    {
        // Configure security directly on the builder
        scriptBox.ConfigureNetwork(net => net.WithAllowedDomains("api.weather.gov"));
        scriptBox.ConfigureFileSystem(fs => fs.WithRootDirectory("./safe-root"));

        // Register plugins to make available as js apis
        scriptBox.RegisterSemanticKernelPlugin<ClockPlugin>("time");
    }
);

var kernel = builder.Build();

var chat = kernel.GetRequiredService<IChatCompletionService>();
var response = await chat.GetChatMessageContentsAsync(
    new ChatHistory("Call scriptbox.run_js with `time.get_current_time()` and report the result."),
    executionSettings: null, // supply your connector's "auto tool" settings here
    kernel);

Console.WriteLine(response.LastOrDefault()?.Content);

public sealed class ClockPlugin
{
    [KernelFunction("get_current_time")]
    [Description("Returns the current UTC time in ISO-8601 format.")]
    public Task<string> GetCurrentTimeAsync()
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        return Task.FromResult(now);
    }
}
```

This highlights the important parts—wiring ScriptBox into Semantic Kernel, registering plugins as namespaces, and letting the LLM choose the `scriptbox.run_js` function. Check the full example in `Examples/Scriptbox.SemanticKernel.Example` if you need a complete console app with extra logging and prompt helpers.

If you still need to feed type information to the LLM, call `SemanticKernelTypeScriptGenerator.Generate(...)` with the namespaces returned from `RegisterSemanticKernelPlugin` and send the resulting `.d.ts` file alongside the instructions.

The generated declaration file contains one interface per namespace plus matching global variables (`time` in the example). In SK orchestration you send this `.d.ts` contents to the model, the model emits JavaScript that relies on those namespaces, and then you call `await scriptBoxPlugin.RunJavaScriptAsync(code, inputJson)` (or the `scriptbox.run_js` tool) to execute it safely.

Need a working sample? `Examples/Scriptbox.SemanticKernel.Example/Program.cs` spins up a Semantic Kernel configured with a chat completion service, wires ScriptBox through `KernelSetup.AddScriptBox`, registers a `ClockPlugin` namespace, and then invokes a tool helper that asks the model to call back into `describeCurrentTime` via JavaScript. Run it to see an end-to-end Semantic Kernel ↔ ScriptBox loop.

## Host API design

* Annotate static or instance classes with `[SandboxApi("namespace")]`.
* Mark public methods with `[SandboxMethod("methodName")]`.
* Parameters are inferred by name; `CancellationToken` and `HostCallContext` can also be injected.
* The builder automatically generates the JavaScript bootstrap code so user scripts can call `namespace.method()` immediately.

## CI & Release

* `.github/workflows/ci.yml` builds and tests the entire solution on every push/PR using .NET 9 & 10 SDKs.
* `.github/workflows/prerelease.yml` watches tags (`v*-beta.*`, etc.), packs all NuGet packages (`ScriptBox`, `ScriptBox.DependencyInjection`, and `ScriptBox.SemanticKernel`), and pushes them to nuget.org.

## Repository Structure

```
ScriptBox/                          # Core runtime
ScriptBox.DependencyInjection/      # Optional DI helpers
ScriptBox.SemanticKernel/           # Semantic Kernel integration
ScriptBox.Tests/                    # xUnit test suite
ScriptBox.SemanticKernel.Tests/     # Semantic Kernel integration tests
Examples/ScriptBox.Example/         # Basic usage examples
Examples/Scriptbox.SemanticKernel.Example/  # SK integration & benchmarks
docs/                               # Additional documentation (vision)
```

---

Contributions and issues are welcome. See `docs/vision.md` for the long-term roadmap and architectural goals.
