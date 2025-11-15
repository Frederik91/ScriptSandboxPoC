using Worker.Core.WasmExecution;

namespace Worker.Services;

/// <summary>
/// Methods for script execution.
/// Acts as the entry point for running JavaScript code in the sandbox.
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
    /// </summary>
    /// <param name="jsCode">The JavaScript source code to execute.</param>
    /// <exception cref="InvalidOperationException">Thrown when script execution fails.</exception>
    public void RunScript(string jsCode)
    {
        if (string.IsNullOrEmpty(jsCode))
        {
            throw new ArgumentException("JavaScript code cannot be null or empty.", nameof(jsCode));
        }

        _scriptExecutor.ExecuteScript(jsCode);
    }
}
