using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScriptBox.Net.Core.Runtime;

/// <summary>
/// Fluent builder for registering JSON-RPC style host handlers.
/// </summary>
public sealed class HostApiBuilder
{
    private readonly Dictionary<string, Func<HostCallContext, Task<object?>>> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public HostApiBuilder RegisterJsonHandler(
        string methodName,
        Func<HostCallContext, Task<object?>> handler)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            throw new ArgumentException("Method name cannot be null or empty", nameof(methodName));
        }

        _handlers[methodName] = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    internal IReadOnlyDictionary<string, Func<HostCallContext, Task<object?>>> Build()
    {
        return new Dictionary<string, Func<HostCallContext, Task<object?>>>(_handlers, StringComparer.OrdinalIgnoreCase);
    }
}
