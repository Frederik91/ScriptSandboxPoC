using System.Runtime.CompilerServices;
using ScriptBox;

namespace ScriptBox.SemanticKernel;

/// <summary>
/// Module initializer that automatically registers the Semantic Kernel API scanner
/// when the ScriptBox.SemanticKernel package is referenced.
/// This allows RegisterApisFrom to work seamlessly with both [SandboxApi] and [KernelFunction] attributes.
/// </summary>
internal static class SemanticKernelModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        ScriptBoxBuilder.RegisterDefaultScanner(() => new SemanticKernelApiScanner());
    }
}
