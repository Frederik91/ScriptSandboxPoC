using ScriptBox.Net.Core.Runtime;

namespace ScriptBox.Demo;

[SandboxApi("calculator")]
public static class DemoCalculatorApi
{
    [SandboxMethod("add")]
    public static Task<int> AddAsync(int a, int b) => Task.FromResult(a + b);

    [SandboxMethod("subtract")]
    public static Task<int> SubtractAsync(int a, int b) => Task.FromResult(a - b);
}
