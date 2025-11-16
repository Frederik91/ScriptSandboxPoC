using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace ScriptBox.Tests.TestApis.SemanticKernel;

public sealed class TestSemanticKernelPlugin
{
    [KernelFunction("add")]
    [Description("Adds two integers.")]
    public Task<int> AddAsync(int a, int b)
    {
        return Task.FromResult(a + b);
    }

    [KernelFunction("echo")]
    public string Echo(string value, string? suffix = null)
    {
        return suffix is null ? value : value + suffix;
    }
}
