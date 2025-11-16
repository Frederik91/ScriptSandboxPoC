using System;
using System.Threading.Tasks;
using Moq;
using ScriptBox.SemanticKernel;
using ScriptBox.Tests.TestApis.SemanticKernel;

namespace ScriptBox.Tests;

public class SemanticKernelIntegrationTests
{
    private static IScriptBoxToolProvider CreateEmptyToolProvider()
    {
        var mock = new Mock<IScriptBoxToolProvider>();
        mock.Setup(x => x.GetTools()).Returns(new List<ScriptBoxToolDescriptor>());
        return mock.Object;
    }

    [Fact]
    public async Task RegisterSemanticKernelPlugin_ExposesNamespace()
    {
        var builder = ScriptBoxBuilder.Create();
        _ = builder.RegisterSemanticKernelPlugin<TestSemanticKernelPlugin>("math");
        await using var scriptBox = builder.Build();

        await using var session = scriptBox.CreateSession();
        var result = await session.RunAsync("return math.add(4, 6);");
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
        var toolProvider = CreateEmptyToolProvider();
        var plugin = new ScriptBoxPlugin(scriptBox, toolProvider);

        var result = await plugin.RunJavaScriptAsync("return ({ total: 21 + 21 })");
        Assert.Equal("{\"total\":42}", result);
    }

    [Fact]
    public async Task ScriptBoxPlugin_PassesInputPayload()
    {
        await using var scriptBox = ScriptBoxBuilder.Create().Build();
        var toolProvider = CreateEmptyToolProvider();
        var plugin = new ScriptBoxPlugin(scriptBox, toolProvider);

        var result = await plugin.RunJavaScriptAsync("return scriptBoxInput.user.value * 2", "{\"value\":5}");
        Assert.Equal("10", result);
    }
}
