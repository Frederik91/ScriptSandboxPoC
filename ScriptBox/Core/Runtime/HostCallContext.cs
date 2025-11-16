using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace ScriptBox.Core.Runtime;

/// <summary>
/// Provides strongly-typed access to host call payloads originating from the WASM sandbox.
/// </summary>
public sealed class HostCallContext
{
    private HostCallContext(
        string method,
        IReadOnlyList<object?> arguments,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        Method = method;
        Arguments = arguments;
        Params = parameters;
        CancellationToken = cancellationToken;
    }

    public string Method { get; }
    public IReadOnlyList<object?> Arguments { get; }
    public IReadOnlyList<object?> Args => Arguments;
    public IReadOnlyDictionary<string, object?> Params { get; }
    public CancellationToken CancellationToken { get; }

    internal static HostCallContext FromJson(
        string method,
        JsonElement root,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<object?> args = Array.Empty<object?>();
        if (root.TryGetProperty("args", out var argsElement) &&
            argsElement.ValueKind == JsonValueKind.Array)
        {
            args = ConvertArray(argsElement);
        }

        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("params", out var paramsElement) &&
            paramsElement.ValueKind == JsonValueKind.Object)
        {
            parameters = ConvertObject(paramsElement);
        }

        return new HostCallContext(method, args, parameters, cancellationToken);
    }

    private static object? ConvertValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l)
                ? l
                : element.TryGetDouble(out var dbl)
                    ? dbl
                    : element.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => ConvertObject(element),
            JsonValueKind.Array => ConvertArray(element),
            _ => element.GetRawText()
        };
    }

    private static IReadOnlyList<object?> ConvertArray(JsonElement element)
    {
        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(ConvertValue(item));
        }
        return list;
    }

    private static Dictionary<string, object?> ConvertObject(JsonElement element)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = ConvertValue(prop.Value);
        }

        return dict;
    }
}
