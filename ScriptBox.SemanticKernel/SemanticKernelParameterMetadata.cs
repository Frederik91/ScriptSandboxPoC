namespace ScriptBox.SemanticKernel;

/// <summary>
/// Describes a Semantic Kernel function parameter for TypeScript generation.
/// </summary>
public sealed class SemanticKernelParameterMetadata
{
    public SemanticKernelParameterMetadata(
        string name,
        string type,
        bool isOptional,
        string? description)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        IsOptional = isOptional;
        Description = description;
    }

    public string Name { get; }

    public string Type { get; }

    public bool IsOptional { get; }

    public string? Description { get; }
}
