using Microsoft.SemanticKernel;
using ScriptBox.SemanticKernel;

namespace Scriptbox.SemanticKernel.Example;

/// <summary>
/// Creates a kernel configured with OpenAI chat completion and ScriptBox support.
/// </summary>
internal static class KernelSetup
{
    public static Kernel BuildKernel(string apiKey, string modelId)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key must be provided.", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model must be provided.", nameof(modelId));
        }

        var builder = Kernel.CreateBuilder();

        builder.AddOpenAIChatCompletion(
            modelId: modelId,
            apiKey: apiKey);

        builder.AddScriptBox(scriptBox =>
        {
            scriptBox.RegisterApisFrom<ClockPlugin>("time");
            scriptBox.RegisterApisFrom<ManyApisPlugin>("utils");
        });

        return builder.Build();
    }
}
