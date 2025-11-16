using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using ScriptBox.SemanticKernel;

namespace Scriptbox.SemanticKernel.Example;

/// <summary>
/// Creates a kernel configured with Ollama chat completion and ScriptBox support.
/// </summary>
internal static class KernelSetup
{
    public static Kernel BuildKernel(Uri endpoint, string modelId)
    {
        if (endpoint is null)
        {
            throw new ArgumentNullException(nameof(endpoint));
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model must be provided.", nameof(modelId));
        }

        var builder = Kernel.CreateBuilder();

        builder.AddOllamaChatCompletion(
            modelId: modelId,
            endpoint: endpoint,
            serviceId: "ollama-chat");

        builder.AddScriptBox(scriptBox =>
        {
            scriptBox.RegisterSemanticKernelPlugin<ClockPlugin>("time");
            scriptBox.RegisterSemanticKernelPlugin<ManyApisPlugin>("utils");
        });

        return builder.Build();
    }
}
