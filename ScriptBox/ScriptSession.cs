using System;
using System.Threading;
using System.Threading.Tasks;
using ScriptBox.Core.WasmExecution;

namespace ScriptBox;

/// <summary>
/// Represents an isolated execution context for running user scripts.
/// </summary>
public sealed class ScriptSession : IAsyncDisposable
{
    private readonly IWasmScriptExecutor _executor;
    private readonly string _bootstrapCode;
    private readonly TimeSpan _timeout;

    internal ScriptSession(
        IWasmScriptExecutor executor,
        string bootstrapCode,
        TimeSpan timeout)
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
        var executionResult = _executor.ExecuteScript(script, timeoutMs);

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<object?>(executionResult.Result);
    }

    /// <summary>
    /// Executes a script and returns the result along with any console logs captured during execution.
    /// </summary>
    /// <param name="userScript">The script to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the return value and logs.</returns>
    public Task<ScriptExecutionResult> ExecuteAsync(string userScript, CancellationToken cancellationToken = default)
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
        var executionResult = _executor.ExecuteScript(script, timeoutMs);

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ScriptExecutionResult
        {
            Result = executionResult.Result,
            Logs = executionResult.Logs
        });
    }

    public ValueTask DisposeAsync()
    {
#if NETSTANDARD2_0
        return default;
#elif NETSTANDARD2_1
        return new ValueTask(Task.CompletedTask);
#else
        return ValueTask.CompletedTask;
#endif
    }

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
