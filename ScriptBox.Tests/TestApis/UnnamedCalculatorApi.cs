using ScriptBox.Core.Runtime;

namespace ScriptBox.Tests.TestApis;

// No [SandboxApi] attribute
public class UnnamedCalculatorApi
{
    [SandboxMethod("add")]
    public int Add(int a, int b) => a + b;
}
