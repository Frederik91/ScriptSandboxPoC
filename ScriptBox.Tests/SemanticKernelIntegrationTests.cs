using System;
using System.Threading.Tasks;
using ScriptBox.SemanticKernel;
using ScriptBox.Tests.TestApis.SemanticKernel;

namespace ScriptBox.Tests;

public class SemanticKernelIntegrationTests
{
    [Fact]
    public async Task RegisterSemanticKernelPlugin_ExposesNamespace()
    {
        var builder = ScriptBoxBuilder.Create();
        _ = builder.RegisterSemanticKernelPlugin<TestSemanticKernelPlugin>("math");
        await using var scriptBox = builder.Build();

        await using var session = scriptBox.CreateSession();
        var result = await session.RunAsync("math.add(4, 6);");
        Assert.Equal(10, Convert.ToInt32(result));
    }

    [Fact]
    public void TypeScriptGenerator_ProducesDeclarations()
    {
        var builder = ScriptBoxBuilder.Create();
        var metadata = builder.RegisterSemanticKernelPlugin<TestSemanticKernelPlugin>("math");
        var dts = SemanticKernelTypeScriptGenerator.Generate(new[] { metadata });

        Assert.Contains("interface MathApi", dts);
        Assert.Contains("add(a: number, b: number): Promise<number>;", dts);
        Assert.Contains("declare const math: ScriptBox.MathApi;", dts);
    }

    [Fact]
    public async Task ScriptBoxPlugin_RunJavaScriptAsync_ReturnsSerializedResult()
    {
        await using var scriptBox = ScriptBoxBuilder.Create().Build();
        var plugin = new ScriptBoxPlugin(scriptBox);

        var result = await plugin.RunJavaScriptAsync("({ total: 21 + 21 })");
        Assert.Equal("{\"total\":42}", result);
    }

    [Fact]
    public async Task ScriptBoxPlugin_PassesInputPayload()
    {
        await using var scriptBox = ScriptBoxBuilder.Create().Build();
        var plugin = new ScriptBoxPlugin(scriptBox);

        var result = await plugin.RunJavaScriptAsync("scriptBoxInput.value * 2", "{\"value\":5}");
        Assert.Equal("10", result);
    }
}
