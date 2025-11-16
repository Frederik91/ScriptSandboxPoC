using System.Collections.Generic;

namespace ScriptBox.SemanticKernel;

/// <summary>
/// Describes a single Semantic Kernel function exported to ScriptBox.
/// </summary>
public sealed class SemanticKernelFunctionMetadata
{
    public SemanticKernelFunctionMetadata(
        string name,
        string? description,
        IReadOnlyList<SemanticKernelParameterMetadata> parameters,
        string returnType,
        bool returnsVoid)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
        ReturnsVoid = returnsVoid;
    }

    /// <summary>
    /// Gets the JavaScript method name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the description of the function if supplied via attributes.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the parameters for this function.
    /// </summary>
    public IReadOnlyList<SemanticKernelParameterMetadata> Parameters { get; }

    /// <summary>
    /// Gets the resolved TypeScript type for the function result (before Promise wrapping).
    /// </summary>
    public string ReturnType { get; }

    /// <summary>
    /// Gets a value indicating whether the method produces a non-value result.
    /// </summary>
    public bool ReturnsVoid { get; }
}
