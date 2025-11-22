using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Scriptbox.SemanticKernel.Example;
using ScriptBox;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

string apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey not found in user secrets.");
string modelId = config["OpenAI:ModelId"] ?? "gpt-5-mini-2025-08-07";

await RunPerformanceBenchmarkAsync(apiKey, modelId);

static async Task RunPerformanceBenchmarkAsync(string apiKey, string modelId)
{
    // Create kernels optimized for each approach
    var toolCallingKernel = PerformanceBenchmark.CreateToolCallingKernel(apiKey, modelId);
    var jsApiKernel = PerformanceBenchmark.CreateJavaScriptApiKernel(apiKey, modelId);

    var chat = toolCallingKernel.GetRequiredService<IChatCompletionService>();
    var tokenSink = toolCallingKernel.GetRequiredService<TokenSink>();
    var benchmark = new PerformanceBenchmark(chat, jsApiKernel, tokenSink);

    // Example 1: Calculate complex math operations
    var mathTask = """
        Calculate: 
        1. Take the numbers 42 and 8
        2. Add them together
        3. Multiply the result by 2
        4. Take the square root
        5. Round to the nearest integer
        6. Check if it's even
        7. If even, multiply by 3, otherwise multiply by 5
        Return the final result.
        """;

    await benchmark.CompareApproachesAsync(toolCallingKernel, mathTask);

    Console.WriteLine("\n" + new string('=', 68) + "\n");

    // Example 2: String and array manipulation
    var stringTask = """
        Given the text "hello world", perform these operations:
        1. Convert to uppercase
        2. Reverse it
        3. Get its length
        4. Check if it starts with "D"
        5. Replace "W" with "X"
        Then create an array of numbers [1, 2, 3, 4, 5] and:
        6. Find the sum
        7. Find the maximum
        8. Check if it's sorted
        Return all results.
        """;

    await benchmark.CompareApproachesAsync(toolCallingKernel, stringTask);
}
