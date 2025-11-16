using System;
using System.Linq;
using System.Threading.Tasks;
using ScriptBox.SemanticKernel.Tests.TestPlugins;

namespace ScriptBox.SemanticKernel.Tests;

public class SemanticKernelBuilderExtensionsTests
{
    [Fact]
    public async Task RegisterSemanticKernelPlugin_ExposesNamespaceAndMetadata()
    {
        var builder = ScriptBoxBuilder.Create();
        var metadata = builder.RegisterSemanticKernelPlugin<SampleMathPlugin>("math");

        Assert.Equal("math", metadata.Name);
        Assert.Equal(2, metadata.Functions.Count);
        Assert.Contains(metadata.Functions, f => f.Name == "add" && f.Parameters.Count == 2);

        await using var scriptBox = builder.Build();
        await using var session = scriptBox.CreateSession();

        var result = await session.RunAsync("return math.add(3, 4);");
        Assert.Equal(7, Convert.ToInt32(result));
    }

    [Fact]
    public void TypeScriptGenerator_RendersNamespaces()
    {
        var builder = ScriptBoxBuilder.Create();
        var metadata = builder.RegisterSemanticKernelPlugin<SampleMathPlugin>("math");

        var declaration = SemanticKernelTypeScriptGenerator.Generate(new[] { metadata });

        Assert.Contains("interface MathApi", declaration);
        Assert.Contains("add(left: number, right: number): Promise<number>;", declaration);
        Assert.Contains("format_sum(left: number, right: number, prefix?: string): Promise<string>;", declaration);
        Assert.Contains("declare const math: ScriptBox.MathApi;", declaration);
    }

    [Fact]
    public void RegisterSemanticKernelPlugin_PreservesDescriptions()
    {
        var builder = ScriptBoxBuilder.Create();
        var metadata = builder.RegisterSemanticKernelPlugin<SampleMathPlugin>("math");
        var add = metadata.Functions.Single(f => f.Name == "add");

        Assert.Equal("Adds two integers.", add.Description);
        Assert.Equal("number", add.Parameters.First().Type);
    }
}
