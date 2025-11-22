namespace ScriptBox.SemanticKernel;

/// <summary>
/// Describes a tool that can be invoked from JavaScript.
/// SK-agnostic; can represent tools from Semantic Kernel, plain APIs, MCP, etc.
/// </summary>
public sealed record ScriptBoxToolDescriptor(
    /// <summary>
    /// Stable, unique identifier for this tool. Format: "{pluginName}.{functionName}"
    /// Used in wire protocol and for routing invocations.
    /// </summary>
    string Id,

    /// <summary>
    /// JavaScript-safe name for this tool (e.g., "str_uppercase", "math_add").
    /// This is the name users call in their scripts.
    /// </summary>
    string Name,

    /// <summary>
    /// Optional: origin plugin name (e.g., "ManyApis", "ClockPlugin").
    /// Useful for grouping or debugging; not used in wire protocol.
    /// </summary>
    string? Plugin,

    /// <summary>
    /// Human-readable description of what this tool does.
    /// Can be sent to LLMs for tool-use prompting.
    /// </summary>
    string? Description,

    /// <summary>
    /// Parameters this tool accepts.
    /// </summary>
    IReadOnlyList<ScriptBoxParameterDescriptor> Parameters,

    /// <summary>
    /// TypeScript type name for the return value (e.g., "string", "number", "Promise<string>").
    /// </summary>
    string ReturnType = "any"
);

/// <summary>
/// Describes a single parameter for a ScriptBoxTool.
/// </summary>
public sealed record ScriptBoxParameterDescriptor(
    /// <summary>
    /// Parameter name (as it appears in the C# method signature).
    /// </summary>
    string Name,

    /// <summary>
    /// TypeScript type name (e.g., "string", "number", "string[]", "Record<string, any>").
    /// </summary>
    string Type,

    /// <summary>
    /// Whether this parameter is optional (has a default or is nullable).
    /// </summary>
    bool IsOptional = false,

    /// <summary>
    /// Human-readable description of this parameter.
    /// </summary>
    string? Description = null
);
