using ScriptBox.Core.WasmExecution;

namespace ScriptBox.Services;

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
    /// Executes JavaScript code in the WASM sandbox and returns the result.
    /// </summary>
    /// <param name="jsCode">The JavaScript source code to execute.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds. If null, uses the default timeout.</param>
    /// <returns>The result of the JavaScript execution as a string. Primitives are converted to their string representation, objects and arrays are converted to JSON.</returns>
    /// <exception cref="InvalidOperationException">Thrown when script execution fails.</exception>
    /// <exception cref="TimeoutException">Thrown when script execution exceeds the timeout limit.</exception>
    public string RunScript(string jsCode, int? timeoutMs = null)
    {
        if (string.IsNullOrEmpty(jsCode))
        {
            throw new ArgumentException("JavaScript code cannot be null or empty.", nameof(jsCode));
        }

        return _scriptExecutor.ExecuteScript(jsCode, timeoutMs);
    }
}
