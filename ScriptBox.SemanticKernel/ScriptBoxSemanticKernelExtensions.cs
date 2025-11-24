using System.Collections.Generic;
using System.Linq;

namespace ScriptBox.SemanticKernel;

/// <summary>
/// Extension methods for IScriptBox that provide Semantic Kernel-specific functionality.
/// </summary>
public static class ScriptBoxSemanticKernelExtensions
{
    /// <summary>
    /// Gets all Semantic Kernel plugin metadata registered with this ScriptBox instance.
    /// This metadata can be used to generate TypeScript declarations.
    /// </summary>
    /// <param name="scriptBox">The ScriptBox instance.</param>
    /// <returns>A collection of namespace metadata for all registered Semantic Kernel plugins.</returns>
    public static IReadOnlyList<SemanticKernelNamespaceMetadata> GetSemanticKernelMetadata(this IScriptBox scriptBox)
    {
        if (scriptBox is null)
        {
            throw new ArgumentNullException(nameof(scriptBox));
        }

        if (scriptBox.Metadata.TryGetValue("SemanticKernelPlugins", out var value) &&
            value is List<SemanticKernelNamespaceMetadata> list)
        {
            return list.AsReadOnly();
        }

        return new List<SemanticKernelNamespaceMetadata>().AsReadOnly();
    }

    /// <summary>
    /// Generates TypeScript declarations for all registered Semantic Kernel plugins.
    /// </summary>
    /// <param name="scriptBox">The ScriptBox instance.</param>
    /// <returns>A TypeScript declaration string that can be saved to a .d.ts file.</returns>
    public static string GenerateTypeScriptDeclarations(this IScriptBox scriptBox)
    {
        var metadata = GetSemanticKernelMetadata(scriptBox);
        return SemanticKernelTypeScriptGenerator.Generate(metadata);
    }
}
