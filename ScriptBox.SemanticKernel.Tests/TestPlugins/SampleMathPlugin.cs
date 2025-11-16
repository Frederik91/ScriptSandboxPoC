using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace ScriptBox.SemanticKernel.Tests.TestPlugins;

public sealed class SampleMathPlugin
{
    [KernelFunction("add")]
    [Description("Adds two integers.")]
    public Task<int> AddAsync(int left, int right)
    {
        return Task.FromResult(left + right);
    }

    [KernelFunction("format_sum")]
    [Description("Adds two numbers and returns a formatted string.")]
    public string FormatSum(int left, int right, string? prefix = null)
    {
        var value = left + right;
        return string.IsNullOrEmpty(prefix) ? value.ToString() : $"{prefix}{value}";
    }
}
