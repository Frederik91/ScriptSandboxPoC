using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Scriptbox.SemanticKernel.Example;

var endpoint = new Uri("http://localhost:11434");
const string modelId = "qwen2.5";

var kernel = KernelSetup.BuildKernel(endpoint, modelId);

await DemonstrateAiToolInvocationAsync(kernel);

static async Task DemonstrateAiToolInvocationAsync(Kernel kernel)
{
    var chat = kernel.GetRequiredService<IChatCompletionService>();
    var toolChallenge = new OllamaScriptAuthor(chat);
    var message = await toolChallenge.DescribeCurrentTimeAsync(kernel);

    Console.WriteLine("=== Semantic Kernel -> Ollama tool invocation ===");
    Console.WriteLine(message);
    Console.WriteLine();
}
