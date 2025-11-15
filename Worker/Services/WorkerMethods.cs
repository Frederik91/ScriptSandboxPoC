using StreamJsonRpc;
using Worker.Core.RpcClient;
using Worker.Core.WasmExecution;

namespace Worker.Services;

/// <summary>
/// RPC methods exposed to the host for script execution.
/// Acts as the entry point for all host-initiated operations.
/// </summary>
public class WorkerMethods
{
    private readonly IWasmScriptExecutor _scriptExecutor;

    public WorkerMethods(IWasmScriptExecutor scriptExecutor)
    {
        _scriptExecutor = scriptExecutor ?? throw new ArgumentNullException(nameof(scriptExecutor));
    }

    /// <summary>
    /// Executes JavaScript code in the WASM sandbox.
    /// Called by the host over JSON-RPC.
    /// </summary>
    /// <param name="jsCode">The JavaScript source code to execute.</param>
    /// <exception cref="InvalidOperationException">Thrown when script execution fails.</exception>
    [JsonRpcMethod("Worker.RunScript")]
    public void RunScript(string jsCode)
    {
        if (string.IsNullOrEmpty(jsCode))
        {
            throw new ArgumentException("JavaScript code cannot be null or empty.", nameof(jsCode));
        }

        _scriptExecutor.ExecuteScript(jsCode);
    }
}
