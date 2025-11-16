using System;
using ScriptBox.Core.WasmExecution;

namespace ScriptBox;

/// <summary>
/// Represents a compiled ScriptBox runtime. Sessions created from the same
/// instance share the underlying WASM module and host bridge configuration.
/// </summary>
#if NET6_0_OR_GREATER
public sealed class ScriptBox : IAsyncDisposable
{
#else
public sealed class ScriptBox : IDisposable
{
#endif
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

#if NET6_0_OR_GREATER
    public ValueTask DisposeAsync()
    {
        return _executor.DisposeAsync();
    }
#else
    public void Dispose()
    {
        _executor.Dispose();
    }
#endif
}
