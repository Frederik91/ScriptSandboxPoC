using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using ScriptBox;

namespace ScriptBox.SemanticKernel;

/// <summary>
/// Semantic Kernel plugin that exposes a single tool for executing ScriptBox-powered JavaScript.
/// </summary>
public sealed class ScriptBoxPlugin
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IScriptBox _scriptBox;

    public ScriptBoxPlugin(IScriptBox scriptBox)
    {
        _scriptBox = scriptBox ?? throw new ArgumentNullException(nameof(scriptBox));
    }

    /// <summary>
    /// Executes JavaScript code inside ScriptBox and returns the serialized result.
    /// </summary>
    [KernelFunction("run_js")]
    [Description("Executes JavaScript inside ScriptBox. Provide code and optional JSON input payload.")]
    public async Task<string> RunJavaScriptAsync(
        string code,
        string? inputJson = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code must be provided.", nameof(code));
        }

        await using var session = _scriptBox.CreateSession();
        var script = BuildScript(code, inputJson);
        var result = await session.RunAsync(script, cancellationToken).ConfigureAwait(false);
        return SerializeResult(result);
    }

    private static string BuildScript(string code, string? inputJson)
    {
        if (string.IsNullOrWhiteSpace(inputJson))
        {
            return code;
        }

        ValidateJson(inputJson);
        var literal = JsonSerializer.Serialize(inputJson, SerializerOptions);
        var sb = new StringBuilder();
        sb.AppendLine($"const scriptBoxInput = JSON.parse({literal});");
        sb.AppendLine("globalThis.scriptBoxInput = scriptBoxInput;");
        sb.AppendLine(code);
        return sb.ToString();
    }

    private static void ValidateJson(string inputJson)
    {
        try
        {
            using var _ = JsonDocument.Parse(inputJson);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("inputJson must be valid JSON.", nameof(inputJson), ex);
        }
    }

    private static string SerializeResult(object? result)
    {
        if (result is null)
        {
            return "null";
        }

        if (result is string s)
        {
            return s;
        }

        if (result is JsonElement element)
        {
            return element.GetRawText();
        }

        return JsonSerializer.Serialize(result, SerializerOptions);
    }
}
