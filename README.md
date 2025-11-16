# ScriptBox

ScriptBox is a reusable QuickJS-in-WASM sandbox for .NET. It lets you run untrusted JavaScript/TypeScript snippets inside a deterministic WASM runtime while exposing a curated host API written in C#. The runtime ships as a NuGet package and can be configured through a fluent builder; dependency injection support lives in a separate optional package.

## Packages

| Package | Description |
| ------- | ----------- |
| `ScriptBox` | Core runtime, builder, WASM bridge, attribute-based API discovery |
| `ScriptBox.DependencyInjection` | Optional helpers that wire `ScriptBoxBuilder` into `Microsoft.Extensions.DependencyInjection` |

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
    .RegisterApisFrom<FileApi>()             // ScriptBox will Activator.CreateInstance<FileApi>()
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

## Host API design

* Annotate static or instance classes with `[SandboxApi("namespace")]`.
* Mark public methods with `[SandboxMethod("methodName")]`.
* Parameters are inferred by name; `CancellationToken` and `HostCallContext` can also be injected.
* The builder automatically generates the JavaScript bootstrap code so user scripts can call `namespace.method()` immediately.

## CI & Release

* `.github/workflows/ci.yml` builds and tests the entire solution on every push/PR using .NET 9 & 10 SDKs.
* `.github/workflows/prerelease.yml` watches tags (`v*-beta.*`, etc.), packs both NuGet packages (`ScriptBox` and `ScriptBox.DependencyInjection`), and pushes them to nuget.org. See `docs/ci.md` for details.

## Repository Structure

```
ScriptBox/                     # Core runtime
ScriptBox.DependencyInjection/ # Optional DI helpers
ScriptBox.Tests/               # xUnit test suite
ScriptBox.Demo/                # Minimal demo app
docs/                          # Additional documentation (CI, vision, usage)
```

---

Contributions and issues are welcome. See `docs/vision.md` for the long-term roadmap and architectural goals.***
