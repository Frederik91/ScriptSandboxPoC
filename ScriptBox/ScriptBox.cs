using System;
using ScriptBox.Core.WasmExecution;

namespace ScriptBox;

/// <summary>
/// Represents a compiled ScriptBox runtime. Sessions created from the same
/// instance share the underlying WASM module and host bridge configuration.
/// </summary>
public sealed class ScriptBox : IAsyncDisposable
{
    private readonly WasmScriptExecutor _executor;
    private readonly string _bootstrapCode;
    private readonly TimeSpan _defaultTimeout;

    internal ScriptBox(
        WasmScriptExecutor executor,
        string bootstrapCode,
        TimeSpan defaultTimeout)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _bootstrapCode = bootstrapCode ?? string.Empty;
        _defaultTimeout = defaultTimeout;
    }

    public ScriptSession CreateSession(TimeSpan? timeout = null)
    {
        return new ScriptSession(
            _executor,
            _bootstrapCode,
            timeout ?? _defaultTimeout);
    }

    public ValueTask DisposeAsync()
    {
        return _executor.DisposeAsync();
    }
}
