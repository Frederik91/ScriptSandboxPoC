namespace ScriptBox.SemanticKernel;

/// <summary>
/// Abstraction for providing tools that can be invoked from JavaScript.
/// Implementations can discover tools from Semantic Kernel, plain APIs, MCP, etc.
/// </summary>
public interface IScriptBoxToolProvider
{
    /// <summary>
    /// Returns all available tools.
    /// Called once per script execution to build metadata for the sandbox.
    /// </summary>
    IReadOnlyList<ScriptBoxToolDescriptor> GetTools();

    /// <summary>
    /// Invokes a tool by its stable identifier.
    /// </summary>
    /// <param name="toolId">The tool ID in format "{pluginName}.{functionName}"</param>
    /// <param name="args">Positional arguments as an array (or null if no args)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The tool result, or null if the tool returns void</returns>
    /// <exception cref="InvalidOperationException">If tool not found</exception>
    /// <exception cref="ArgumentException">If argument count/types don't match</exception>
    Task<object?> InvokeToolAsync(string toolId, object? args, CancellationToken cancellationToken = default);
}
