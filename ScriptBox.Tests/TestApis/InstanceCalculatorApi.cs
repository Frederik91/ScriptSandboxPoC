using ScriptBox.Core.Runtime;

namespace ScriptBox.Tests.TestApis;

[SandboxApi("instanceCalc")]
public class InstanceCalculatorApi
{
    [SandboxMethod("add")]
    public int Add(int a, int b) => a + b;
}
