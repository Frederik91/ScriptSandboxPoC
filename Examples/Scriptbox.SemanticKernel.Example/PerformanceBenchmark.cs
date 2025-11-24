using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
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
    private readonly Kernel _jsApiKernel;
    private readonly IScriptBox _scriptBox;
    private readonly TokenSink _tokenSink;

    public PerformanceBenchmark(IChatCompletionService chat, Kernel jsApiKernel, TokenSink tokenSink)
    {
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _jsApiKernel = jsApiKernel ?? throw new ArgumentNullException(nameof(jsApiKernel));
        _scriptBox = jsApiKernel.GetRequiredService<IScriptBox>();
        _tokenSink = tokenSink ?? throw new ArgumentNullException(nameof(tokenSink));
    }

    /// <summary>
    /// Creates a kernel with plugins registered as Semantic Kernel tools for function calling.
    /// </summary>
    public static Kernel CreateToolCallingKernel(string apiKey, string modelId)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key must be provided.", nameof(apiKey));
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model must be provided.", nameof(modelId));

        var builder = Kernel.CreateBuilder();

        // --- METRICS SETUP ---
        var sink = new TokenSink();
        builder.Services.AddSingleton(sink);
        
        // Create HTTP client with token counting handler
        var handler = new TokenCountingHandler(sink);
        var httpClient = new HttpClient(handler);
        builder.Services.AddSingleton(httpClient); // Register so we can dispose if needed, though SK might not use this registration directly

        builder.AddOpenAIChatCompletion(
            modelId: modelId,
            apiKey: apiKey,
            httpClient: httpClient);

        // Register plugins as Semantic Kernel tools for tool calling
        builder.Plugins.AddFromType<ManyApisPlugin>("utils");
        builder.Plugins.AddFromType<ClockPlugin>("time");

        return builder.Build();
    }

    /// <summary>
    /// Creates a kernel with plugins exposed via ScriptBox JavaScript API.
    /// </summary>
    public static Kernel CreateJavaScriptApiKernel(string apiKey, string modelId)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key must be provided.", nameof(apiKey));
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model must be provided.", nameof(modelId));

        var builder = Kernel.CreateBuilder();

        builder.AddOpenAIChatCompletion(
            modelId: modelId,
            apiKey: apiKey);

        // Register plugins as JavaScript APIs via ScriptBox
        builder.AddScriptBox(scriptBox =>
        {
            scriptBox.RegisterApisFrom<ClockPlugin>("time");
            scriptBox.RegisterApisFrom<ManyApisPlugin>("sb_utils");
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
        
        // Reset counters before run
        _tokenSink.Reset();

        try
        {
            const string systemPrompt = """
                You have access to 100+ utility tools covering math, string, array, type conversion, 
                data structures, and time operations. 
                Use these tools to solve the given task. Call as many tools as needed.
                Always report your final result.
                """;

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            };

            var history = new ChatHistory(systemPrompt);
            history.AddUserMessage(taskDescription);

            var messages = await _chat
                .GetChatMessageContentsAsync(history, executionSettings, kernel)
                .ConfigureAwait(false);

            // Count total function invocations across all messages (handling parallel calls in single message)
            invocationCount = history
                .SelectMany(m => m.Items ?? Enumerable.Empty<KernelContent>())
                .OfType<FunctionCallContent>()
                .Count();

            stopwatch.Stop();

            var result = messages.LastOrDefault();
            var content = result?.Content ?? "No response";
            
            // Get accurate token count from sink
            var (input, output) = _tokenSink.Snapshot();
            var totalTokens = (int)(input + output);

            return new BenchmarkResult(
                Method: "Tool Calling",
                ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                ToolInvocations: invocationCount,
                Result: content,
                Success: true,
                TotalTokens: totalTokens
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
                Success: false,
                TotalTokens: 0
            );
        }
    }

    /// <summary>
    /// Runs JS API benchmark: AI generates script then executes it.
    /// </summary>
    public async Task<BenchmarkResult> RunJavaScriptApiBenchmarkAsync(string taskDescription)
    {
        if (string.IsNullOrWhiteSpace(taskDescription)) throw new ArgumentException("Task description required.", nameof(taskDescription));

        var stopwatch = Stopwatch.StartNew();
        string generatedScript = "";
        
        // Reset counters before run
        _tokenSink.Reset();

        try
        {
            var systemPrompt = "You are a helpful assistant. Use the available tools to solve the user's request.";

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            };

            var history = new ChatHistory(systemPrompt);
            history.AddUserMessage(taskDescription + "\n\nUse the tools available to solve the user's request.");

            // The kernel has both ScriptBoxPlugin (run_js) and ScriptBoxDiscoveryPlugin (get_schema)
            var messages = await _chat.GetChatMessageContentsAsync(history, executionSettings, _jsApiKernel).ConfigureAwait(false);
            
            stopwatch.Stop();

            var result = messages.LastOrDefault();
            var content = result?.Content ?? "No response";

            // Find the script that was executed (if any)
            var runJsCall = history
                .SelectMany(m => m.Items ?? Enumerable.Empty<KernelContent>())
                .OfType<FunctionCallContent>()
                .FirstOrDefault(f => f.FunctionName == "run_js");
            
            if (runJsCall?.Arguments != null && runJsCall.Arguments.TryGetValue("code", out var codeObj))
            {
                generatedScript = codeObj?.ToString() ?? "";
            }

            // Count tool invocations
            var invocationCount = history
                .SelectMany(m => m.Items ?? Enumerable.Empty<KernelContent>())
                .OfType<FunctionCallContent>()
                .Count();
            
            // Get accurate token count from sink
            var (input, output) = _tokenSink.Snapshot();
            var totalTokens = (int)(input + output);

            return new BenchmarkResult(
                Method: "JavaScript API",
                ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                ToolInvocations: invocationCount,
                Result: content,
                Success: true,
                TotalTokens: totalTokens,
                GeneratedScript: generatedScript
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new BenchmarkResult(
                Method: "JavaScript API",
                ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                ToolInvocations: 0,
                Result: $"Error: {ex.Message}",
                Success: false,
                TotalTokens: 0,
                GeneratedScript: generatedScript
            );
        }
    }

    private int GetTotalTokens(ChatHistory history)
    {
        int total = 0;
        foreach (var message in history)
        {
            if (message.Metadata != null && message.Metadata.TryGetValue("Usage", out var usage) && usage != null)
            {
                try
                {
                    var prop = usage.GetType().GetProperty("TotalTokenCount"); // Try TotalTokenCount first
                    if (prop == null) prop = usage.GetType().GetProperty("TotalTokens"); // Fallback
                    
                    if (prop != null)
                    {
                        total += (int)prop.GetValue(usage)!;
                    }
                }
                catch { }
            }
        }
        return total;
    }



    /// <summary>
    /// Runs a head-to-head comparison of both approaches.
    /// </summary>
    public async Task<ComparisonResult> CompareApproachesAsync(
        Kernel kernel,
        string taskDescription)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       PERFORMANCE BENCHMARK: Tool Calling vs JavaScript API    ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝\n");

        Console.WriteLine("📊 TOOL CALLING APPROACH");
        Console.WriteLine("─────────────────────────");
        Console.WriteLine($"Task: {taskDescription}\n");
        var toolResult = await RunToolCallingBenchmarkAsync(kernel, taskDescription);
        PrintBenchmarkResult(toolResult);

        Console.WriteLine("\n📊 JAVASCRIPT API APPROACH");
        Console.WriteLine("──────────────────────────");
        Console.WriteLine("(Generating and executing script...)\n");
        var jsResult = await RunJavaScriptApiBenchmarkAsync(taskDescription);
        PrintBenchmarkResult(jsResult);

        Console.WriteLine("\n📈 COMPARISON");
        Console.WriteLine("─────────────");

        // Avoid division by zero
        var jsTime = jsResult.ElapsedMilliseconds == 0 ? 1 : jsResult.ElapsedMilliseconds;
        var speedup = (double)toolResult.ElapsedMilliseconds / jsTime;
        var overhead = ((speedup - 1) * 100);

        Console.WriteLine($"Tool Calling Time:      {toolResult.ElapsedMilliseconds} ms");
        Console.WriteLine($"JavaScript API Time:    {jsResult.ElapsedMilliseconds} ms");
        Console.WriteLine($"Speedup Factor:         {speedup:F2}x faster (JS API)");
        Console.WriteLine($"Tool Invocations:       {toolResult.ToolInvocations} vs {jsResult.ToolInvocations}");

        int tokenDiff = toolResult.TotalTokens - jsResult.TotalTokens;
        double tokenReduction = toolResult.TotalTokens > 0 ? (double)tokenDiff / toolResult.TotalTokens * 100 : 0;
        Console.WriteLine($"Total Tokens:           {toolResult.TotalTokens} vs {jsResult.TotalTokens}");
        Console.WriteLine($"Token Reduction:        {tokenReduction:F2}% (saved {tokenDiff} tokens)");

        return new ComparisonResult(toolResult, jsResult, speedup, overhead, tokenDiff, tokenReduction);
    }

    private static void PrintBenchmarkResult(BenchmarkResult result)
    {
        var statusIcon = result.Success ? "✅" : "❌";
        Console.WriteLine($"{statusIcon} Status:          {(result.Success ? "Success" : "Failed")}");
        Console.WriteLine($"⏱️  Time:            {result.ElapsedMilliseconds} ms");
        Console.WriteLine($"🔧 Tool Calls:      {result.ToolInvocations}");
        Console.WriteLine($"🪙 Tokens:          {result.TotalTokens}");
        if (!string.IsNullOrEmpty(result.GeneratedScript))
        {
            Console.WriteLine("📜 Generated Script:");
            Console.WriteLine(result.GeneratedScript);
            Console.WriteLine("--------------------");
        }
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
    bool Success,
    int TotalTokens,
    string? GeneratedScript = null);

/// <summary>
/// Comparison between two benchmark approaches.
/// </summary>
public record ComparisonResult(
    BenchmarkResult ToolCallingResult,
    BenchmarkResult JavaScriptApiResult,
    double SpeedupFactor,
    double OverheadPercent,
    int TokenDiff,
    double TokenReductionPercent);
