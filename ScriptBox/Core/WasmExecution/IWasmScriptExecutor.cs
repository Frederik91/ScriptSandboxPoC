namespace ScriptBox.Core.WasmExecution;

/// <summary>
/// Interface for executing JavaScript code in the WASM sandbox.
/// Abstracts the details of WASM module loading, instantiation, and script execution.
/// </summary>
public interface IWasmScriptExecutor
{
    /// <summary>
    /// Executes JavaScript code in the QuickJS WASM sandbox.
    /// </summary>
    /// <param name="jsCode">The JavaScript source code to execute.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds. If null, uses the default timeout. Set to 0 for no timeout (use with caution).</param>
    /// <exception cref="FileNotFoundException">Thrown when the WASM module cannot be located.</exception>
    /// <exception cref="InvalidOperationException">Thrown when WASM initialization or script execution fails.</exception>
    /// <exception cref="TimeoutException">Thrown when script execution exceeds the timeout limit.</exception>
    void ExecuteScript(string jsCode, int? timeoutMs = null);
}
