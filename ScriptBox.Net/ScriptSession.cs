using System;
using System.Threading;
using System.Threading.Tasks;
using ScriptBox.Net.Core.WasmExecution;

namespace ScriptBox.Net;

/// <summary>
/// Represents an isolated execution context for running user scripts.
/// </summary>
public sealed class ScriptSession : IAsyncDisposable
{
    private readonly IWasmScriptExecutor _executor;
    private readonly string _bootstrapCode;
    private readonly TimeSpan _timeout;

    internal ScriptSession(IWasmScriptExecutor executor, string bootstrapCode, TimeSpan timeout)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _bootstrapCode = bootstrapCode ?? string.Empty;
        _timeout = timeout;
    }

    public Task<object?> RunAsync(string userScript, CancellationToken cancellationToken = default)
    {
        if (userScript is null)
        {
            throw new ArgumentNullException(nameof(userScript));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var script = string.IsNullOrWhiteSpace(_bootstrapCode)
            ? userScript
            : string.Concat(_bootstrapCode, "\n", userScript);

        var timeoutMs = ConvertTimeoutToMilliseconds(_timeout);
        _executor.ExecuteScript(script, timeoutMs);

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<object?>(null);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static int? ConvertTimeoutToMilliseconds(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            return null;
        }

        var ms = timeout.TotalMilliseconds;
        if (ms >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)Math.Ceiling(ms);
    }
}
