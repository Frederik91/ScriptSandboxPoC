using System.Text.Json;
using Moq;

namespace ScriptBox.SemanticKernel.Tests;

public class ScriptBoxPluginTests
{
    private static IScriptBoxToolProvider CreateEmptyToolProvider()
    {
        var mock = new Mock<IScriptBoxToolProvider>();
        mock.Setup(x => x.GetTools()).Returns(new List<ScriptBoxToolDescriptor>());
        return mock.Object;
    }

    [Fact]
    public async Task RunJavaScriptAsync_ReturnsSerializedObject()
    {
        await using var box = ScriptBoxBuilder.Create().Build();
        var toolProvider = CreateEmptyToolProvider();
        var plugin = new ScriptBoxPlugin(box, toolProvider);

        var output = await plugin.RunJavaScriptAsync("return ({ total: 4 + 5 })");

        using var doc = JsonDocument.Parse(output);
        Assert.Equal(9, doc.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task RunJavaScriptAsync_IncludesInputPayload()
    {
        await using var box = ScriptBoxBuilder.Create().Build();
        var toolProvider = CreateEmptyToolProvider();
        var plugin = new ScriptBoxPlugin(box, toolProvider);

        var output = await plugin.RunJavaScriptAsync("return scriptBoxInput.user.value * 3", "{\"value\":7}");

        Assert.Equal("21", output);
    }

    [Fact]
    public async Task RunJavaScriptAsync_InvalidJson_Throws()
    {
        await using var box = ScriptBoxBuilder.Create().Build();
        var toolProvider = CreateEmptyToolProvider();
        var plugin = new ScriptBoxPlugin(box, toolProvider);

        await Assert.ThrowsAsync<ArgumentException>(() => plugin.RunJavaScriptAsync("1+1", "not-json"));
    }
}
