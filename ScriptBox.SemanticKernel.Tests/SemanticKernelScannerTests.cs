using System.ComponentModel;
using Microsoft.SemanticKernel;
using ScriptBox.Core.Runtime;

namespace ScriptBox.SemanticKernel.Tests;

public class SemanticKernelScannerTests
{
    [SandboxApi("sk_plugin")]
    public class MySkPlugin
    {
        [KernelFunction("add")]
        [Description("Adds two numbers")]
        public int Add(int a, int b) => a + b;
    }

    [Fact]
    public async Task CanRegisterPluginUsingScanner()
    {
        var builder = ScriptBoxBuilder.Create()
            .WithApiScanner(new SemanticKernelApiScanner())
            .RegisterApisFrom<MySkPlugin>();

        await using var box = builder.Build();
        await using var session = box.CreateSession();

        var result = await session.RunAsync("return sk_plugin.add(10, 20);");
        Assert.Equal("30", result);
    }

    [Fact]
    public async Task CanRegisterPluginWithoutExplicitScanner_UsingModuleInitializer()
    {
        // This test verifies that the SemanticKernelApiScanner is automatically registered
        // via the module initializer, so users don't need to call WithApiScanner
        var builder = ScriptBoxBuilder.Create()
            .RegisterApisFrom<MySkPlugin>(); // No explicit WithApiScanner call!

        await using var box = builder.Build();
        await using var session = box.CreateSession();

        var result = await session.RunAsync("return sk_plugin.add(15, 25);");
        Assert.Equal("40", result);
    }
}
