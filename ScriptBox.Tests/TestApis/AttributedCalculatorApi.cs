using System.Threading.Tasks;
using ScriptBox.Core.Runtime;

namespace ScriptBox.Tests.TestApis;

[SandboxApi("calculator")]
public static class AttributedCalculatorApi
{
    [SandboxMethod("add")]
    public static Task<int> AddAsync(int a, int b) => Task.FromResult(a + b);
}
