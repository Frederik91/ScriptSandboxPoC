using System.Collections.Generic;

namespace ScriptBox.SemanticKernel;

/// <summary>
/// Describes a ScriptBox namespace that originated from an annotated Semantic Kernel plugin.
/// </summary>
public sealed class SemanticKernelNamespaceMetadata
{
    public SemanticKernelNamespaceMetadata(
        string name,
        IReadOnlyList<SemanticKernelFunctionMetadata> functions)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Functions = functions ?? throw new ArgumentNullException(nameof(functions));
    }

    /// <summary>
    /// Gets the JavaScript namespace exposed to ScriptBox.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the functions that belong to this namespace.
    /// </summary>
    public IReadOnlyList<SemanticKernelFunctionMetadata> Functions { get; }
}
