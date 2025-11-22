using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScriptBox.SemanticKernel;

/// <summary>
/// Provides tools that have been registered directly into ScriptBox via RegisterSemanticKernelPlugin.
/// These tools are available as host APIs in the sandbox.
/// </summary>
public sealed class ScriptBoxMetadataToolProvider : IScriptBoxToolProvider
{
    private readonly IScriptBox _scriptBox;

    public ScriptBoxMetadataToolProvider(IScriptBox scriptBox)
    {
        _scriptBox = scriptBox ?? throw new ArgumentNullException(nameof(scriptBox));
    }

    public IReadOnlyList<ScriptBoxToolDescriptor> GetTools()
    {
        if (!_scriptBox.Metadata.TryGetValue("SemanticKernelPlugins", out var value) ||
            value is not List<SemanticKernelNamespaceMetadata> plugins)
        {
            return Array.Empty<ScriptBoxToolDescriptor>();
        }

        var tools = new List<ScriptBoxToolDescriptor>();

        foreach (var plugin in plugins)
        {
            foreach (var function in plugin.Functions)
            {
                var toolId = $"{plugin.Name}.{function.Name}";
                var parameters = function.Parameters.Select(p => new ScriptBoxParameterDescriptor(
                    Name: p.Name,
                    Type: p.Type,
                    IsOptional: p.IsOptional,
                    Description: p.Description
                )).ToList();

                tools.Add(new ScriptBoxToolDescriptor(
                    Id: toolId,
                    Name: function.Name,
                    Plugin: plugin.Name,
                    Description: function.Description,
                    Parameters: parameters,
                    ReturnType: function.ReturnType
                ));
            }
        }

        return tools;
    }

    public Task<object?> InvokeToolAsync(string toolId, object? args, CancellationToken cancellationToken = default)
    {
        // This provider describes tools that are available as HOST APIs in the sandbox.
        // They are invoked by the JS code calling the host API.
        // The LLM does NOT invoke them directly via this provider.
        throw new NotSupportedException("Tools described by ScriptBoxMetadataToolProvider are invoked directly by the JavaScript code in the sandbox.");
    }
}
