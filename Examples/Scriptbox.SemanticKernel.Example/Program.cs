using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Scriptbox.SemanticKernel.Example;
using ScriptBox;

var endpoint = new Uri("http://localhost:11434");
const string modelId = "qwen2.5";

var kernel = KernelSetup.BuildKernel(endpoint, modelId);

// Uncomment one of these to run:
// await DemonstrateAiToolInvocationAsync(kernel);
await RunPerformanceBenchmarkAsync(kernel);

static async Task DemonstrateAiToolInvocationAsync(Kernel kernel)
{
    var chat = kernel.GetRequiredService<IChatCompletionService>();
    var toolChallenge = new OllamaScriptAuthor(chat);
    var message = await toolChallenge.DescribeCurrentTimeAsync(kernel);

    Console.WriteLine("=== Semantic Kernel -> Ollama tool invocation ===");
    Console.WriteLine(message);
    Console.WriteLine();
}

static async Task RunPerformanceBenchmarkAsync(Kernel kernel)
{
    var endpoint = new Uri("http://localhost:11434");
    const string modelId = "qwen2.5";

    // Create kernels optimized for each approach
    var toolCallingKernel = PerformanceBenchmark.CreateToolCallingKernel(endpoint, modelId);
    var jsApiKernel = PerformanceBenchmark.CreateJavaScriptApiKernel(endpoint, modelId);

    var chat = toolCallingKernel.GetRequiredService<IChatCompletionService>();
    var scriptBox = jsApiKernel.GetRequiredService<IScriptBox>();
    var benchmark = new PerformanceBenchmark(chat, scriptBox);

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

    var mathScript = """
        const utils = assistantApi.utils;
        let a = 42, b = 8;
        let sum = utils.math_add(a, b);
        let doubled = utils.math_multiply(sum, 2);
        let sqrted = utils.math_square_root(doubled);
        let rounded = utils.math_round(sqrted);
        let isEven = utils.util_is_even(rounded);
        let final = isEven ? utils.math_multiply(rounded, 3) : utils.math_multiply(rounded, 5);
        return { result: final, steps: 7 };
        """;

    await benchmark.CompareApproachesAsync(toolCallingKernel, mathTask, mathScript);

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

    var stringScript = """
        const utils = assistantApi.utils;
        let text = "hello world";
        let upper = utils.str_uppercase(text);
        let reversed = utils.str_reverse(upper);
        let len = utils.str_length(reversed);
        let startsD = utils.str_starts_with(reversed, "D");
        let replaced = utils.str_replace(reversed, "W", "X");
        
        let arr = [1, 2, 3, 4, 5];
        let sum = utils.array_sum(arr);
        let max = utils.array_max(arr);
        let sorted = utils.array_is_sorted(arr);
        
        return { text_ops: [upper, reversed, len, startsD, replaced], array_ops: [sum, max, sorted] };
        """;

    await benchmark.CompareApproachesAsync(toolCallingKernel, stringTask, stringScript);
}
