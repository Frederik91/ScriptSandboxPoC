using System.Text.Json;

namespace ScriptBox.SemanticKernel.Tests;

public class ScriptBoxPluginTests
{
    [Fact]
    public async Task RunJavaScriptAsync_ReturnsSerializedObject()
    {
        await using var box = ScriptBoxBuilder.Create().Build();
        var plugin = new ScriptBoxPlugin(box);

        var output = await plugin.RunJavaScriptAsync("({ total: 4 + 5 })");

        using var doc = JsonDocument.Parse(output);
        Assert.Equal(9, doc.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task RunJavaScriptAsync_IncludesInputPayload()
    {
        await using var box = ScriptBoxBuilder.Create().Build();
        var plugin = new ScriptBoxPlugin(box);

        var output = await plugin.RunJavaScriptAsync("scriptBoxInput.value * 3", "{\"value\":7}");

        Assert.Equal("21", output);
    }

    [Fact]
    public async Task RunJavaScriptAsync_InvalidJson_Throws()
    {
        await using var box = ScriptBoxBuilder.Create().Build();
        var plugin = new ScriptBoxPlugin(box);

        await Assert.ThrowsAsync<ArgumentException>(() => plugin.RunJavaScriptAsync("1+1", "not-json"));
    }
}
