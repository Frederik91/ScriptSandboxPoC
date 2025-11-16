using Microsoft.SemanticKernel;
using ScriptBox.SemanticKernel.Tests.TestPlugins;

namespace ScriptBox.SemanticKernel.Tests;

public class KernelBuilderScriptBoxExtensionsTests
{
    [Fact]
    public async Task AddScriptBox_RegistersPluginAndRunsScripts()
    {
        var builder = Kernel.CreateBuilder();

        builder.AddScriptBox(scriptBox =>
        {
            scriptBox.RegisterSemanticKernelPlugin<SampleMathPlugin>("math");
        });

        var kernel = builder.Build();

        const string script = """
            const total = math.add(2, 3);
            total;
            """;

        var arguments = new KernelArguments
        {
            ["code"] = script
        };

        var result = await kernel.InvokeAsync<string>("scriptbox", "run_js", arguments);

        Assert.Equal("5", result);
    }
}
