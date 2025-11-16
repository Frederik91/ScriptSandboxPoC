using System.Diagnostics;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using ScriptBox;
using ScriptBox.SemanticKernel;

namespace Scriptbox.SemanticKernel.Example;

/// <summary>
/// Benchmarks performance of tool calling vs direct JavaScript API execution.
/// Demonstrates the overhead of Semantic Kernel's function invocation pipeline.
/// </summary>
internal sealed class PerformanceBenchmark
{
    private readonly IChatCompletionService _chat;
    private readonly IScriptBox _scriptBox;

    public PerformanceBenchmark(IChatCompletionService chat, IScriptBox scriptBox)
    {
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _scriptBox = scriptBox ?? throw new ArgumentNullException(nameof(scriptBox));
    }

    /// <summary>
    /// Creates a kernel with plugins registered as Semantic Kernel tools for function calling.
    /// </summary>
    public static Kernel CreateToolCallingKernel(Uri endpoint, string modelId)
    {
        if (endpoint is null)
            throw new ArgumentNullException(nameof(endpoint));
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model must be provided.", nameof(modelId));

        var builder = Kernel.CreateBuilder();

        builder.AddOllamaChatCompletion(
            modelId: modelId,
            endpoint: endpoint,
            serviceId: "ollama-chat");

        // Register plugins as Semantic Kernel tools for tool calling
        builder.Plugins.AddFromType<ManyApisPlugin>("utils");
        builder.Plugins.AddFromType<ClockPlugin>("time");

        return builder.Build();
    }

    /// <summary>
    /// Creates a kernel with plugins exposed via ScriptBox JavaScript API.
    /// </summary>
    public static Kernel CreateJavaScriptApiKernel(Uri endpoint, string modelId)
    {
        if (endpoint is null)
            throw new ArgumentNullException(nameof(endpoint));
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model must be provided.", nameof(modelId));

        var builder = Kernel.CreateBuilder();

        builder.AddOllamaChatCompletion(
            modelId: modelId,
            endpoint: endpoint,
            serviceId: "ollama-chat");

        // Register plugins as JavaScript APIs via ScriptBox
        builder.AddScriptBox(scriptBox =>
        {
            scriptBox.RegisterSemanticKernelPlugin<ClockPlugin>("time");
            scriptBox.RegisterSemanticKernelPlugin<ManyApisPlugin>("utils");
        });

        return builder.Build();
    }

    /// <summary>
    /// Runs tool-calling benchmark: LLM invokes many Semantic Kernel tools.
    /// </summary>
    public async Task<BenchmarkResult> RunToolCallingBenchmarkAsync(Kernel kernel, string taskDescription)
    {
        if (kernel is null) throw new ArgumentNullException(nameof(kernel));
        if (string.IsNullOrWhiteSpace(taskDescription)) throw new ArgumentException("Task description required.", nameof(taskDescription));

        var stopwatch = Stopwatch.StartNew();
        var invocationCount = 0;

        try
        {
            const string systemPrompt = """
                You have access to 100+ utility tools covering math, string, array, type conversion, 
                data structures, and time operations. 
                Use these tools to solve the given task. Call as many tools as needed.
                Always report your final result.
                """;

            var executionSettings = new OllamaPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature = 0.7f,
                TopP = 0.9f
            };

            var history = new ChatHistory(systemPrompt);
            history.AddUserMessage(taskDescription);

            var messages = await _chat
                .GetChatMessageContentsAsync(history, executionSettings, kernel)
                .ConfigureAwait(false);

            invocationCount = messages.Count;

            stopwatch.Stop();

            var result = messages.LastOrDefault();
            var content = result?.Content ?? "No response";

            return new BenchmarkResult(
                Method: "Tool Calling",
                ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                ToolInvocations: invocationCount,
                Result: content,
                Success: true
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new BenchmarkResult(
                Method: "Tool Calling",
                ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                ToolInvocations: invocationCount,
                Result: $"Error: {ex.Message}",
                Success: false
            );
        }
    }

    /// <summary>
    /// Runs JS API benchmark: direct JavaScript execution using ScriptBox.
    /// </summary>
    public async Task<BenchmarkResult> RunJavaScriptApiBenchmarkAsync(string jsScript)
    {
        if (string.IsNullOrWhiteSpace(jsScript)) throw new ArgumentException("JS script required.", nameof(jsScript));

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var session = _scriptBox.CreateSession();
            var result = await session.RunAsync(jsScript).ConfigureAwait(false);

            stopwatch.Stop();

            return new BenchmarkResult(
                Method: "JavaScript API",
                ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                ToolInvocations: 1, // Single JS execution
                Result: result?.ToString() ?? "No result",
                Success: true
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new BenchmarkResult(
                Method: "JavaScript API",
                ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                ToolInvocations: 1,
                Result: $"Error: {ex.Message}",
                Success: false
            );
        }
    }

    /// <summary>
    /// Runs a head-to-head comparison of both approaches.
    /// </summary>
    public async Task<ComparisonResult> CompareApproachesAsync(
        Kernel kernel,
        string toolCallingTask,
        string jsApiScript)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       PERFORMANCE BENCHMARK: Tool Calling vs JavaScript API    ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝\n");

        Console.WriteLine("📊 TOOL CALLING APPROACH");
        Console.WriteLine("─────────────────────────");
        Console.WriteLine($"Task: {toolCallingTask}\n");
        var toolResult = await RunToolCallingBenchmarkAsync(kernel, toolCallingTask);
        PrintBenchmarkResult(toolResult);

        Console.WriteLine("\n📊 JAVASCRIPT API APPROACH");
        Console.WriteLine("──────────────────────────");
        Console.WriteLine($"Script:\n{jsApiScript}\n");
        var jsResult = await RunJavaScriptApiBenchmarkAsync(jsApiScript);
        PrintBenchmarkResult(jsResult);

        Console.WriteLine("\n📈 COMPARISON");
        Console.WriteLine("─────────────");

        var speedup = (double)toolResult.ElapsedMilliseconds / jsResult.ElapsedMilliseconds;
        var overhead = ((speedup - 1) * 100);

        Console.WriteLine($"Tool Calling Time:      {toolResult.ElapsedMilliseconds} ms");
        Console.WriteLine($"JavaScript API Time:    {jsResult.ElapsedMilliseconds} ms");
        Console.WriteLine($"Overhead Factor:        {speedup:F2}x slower");
        Console.WriteLine($"Absolute Overhead:      {overhead:F1}%");
        Console.WriteLine($"Tool Invocations:       {toolResult.ToolInvocations}");

        return new ComparisonResult(toolResult, jsResult, speedup, overhead);
    }

    private static void PrintBenchmarkResult(BenchmarkResult result)
    {
        var statusIcon = result.Success ? "✅" : "❌";
        Console.WriteLine($"{statusIcon} Status:          {(result.Success ? "Success" : "Failed")}");
        Console.WriteLine($"⏱️  Time:            {result.ElapsedMilliseconds} ms");
        Console.WriteLine($"🔧 Tool Calls:      {result.ToolInvocations}");
        Console.WriteLine($"📝 Result Preview:  {(result.Result?.Length > 100 ? result.Result.Substring(0, 97) + "..." : result.Result)}");
    }
}

/// <summary>
/// Result of a single benchmark run.
/// </summary>
public record BenchmarkResult(
    string Method,
    long ElapsedMilliseconds,
    int ToolInvocations,
    string Result,
    bool Success);

/// <summary>
/// Comparison between two benchmark approaches.
/// </summary>
public record ComparisonResult(
    BenchmarkResult ToolCallingResult,
    BenchmarkResult JavaScriptApiResult,
    double SpeedupFactor,
    double OverheadPercent);
