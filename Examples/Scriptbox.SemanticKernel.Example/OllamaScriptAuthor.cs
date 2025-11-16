using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;

namespace Scriptbox.SemanticKernel.Example;

/// <summary>
/// Challenges the LLM to call the ScriptBox tool directly via Semantic Kernel function calling.
/// </summary>
internal sealed class OllamaScriptAuthor
{
    private readonly IChatCompletionService _chat;

    public OllamaScriptAuthor(IChatCompletionService chat)
    {
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
    }

    public async Task<string> DescribeCurrentTimeAsync(Kernel kernel)
    {
        if (kernel is null)
        {
            throw new ArgumentNullException(nameof(kernel));
        }

        const string prompt = """
            You can execute JavaScript by invoking the tool scriptbox.run_js.
            Goal: determine the current UTC time using the ScriptBox API `time.get_current_time()`.
            Steps:
            1. Call scriptbox.run_js with a JavaScript snippet that calls `time.get_current_time()` and returns the value.
            2. After receiving the tool result, respond with a friendly sentence stating the timestamp you observed.
            Always invoke the tool before answering.
            """;

        var executionSettings = new OllamaPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var history = new ChatHistory(prompt);

        var messages = await _chat
            .GetChatMessageContentsAsync(history, executionSettings, kernel)
            .ConfigureAwait(false);

        var result = messages.LastOrDefault();

        if (result is null)
        {
            return string.Empty;
        }

        return result.Content ?? result.ToString() ?? string.Empty;
    }
}
