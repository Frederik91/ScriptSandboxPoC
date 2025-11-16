using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Scriptbox.SemanticKernel.Example;

/// <summary>
/// Provides current time data to scripts via Semantic Kernel.
/// </summary>
public sealed class ClockPlugin
{
    [KernelFunction("get_current_time")]
    [Description("Returns the current UTC time in ISO-8601 format.")]
    public Task<string> GetCurrentTimeAsync()
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        return Task.FromResult(now);
    }
}
