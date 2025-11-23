namespace ScriptBox.Core.WasmExecution;

/// <summary>
/// Interface for executing JavaScript code in the WASM sandbox.
/// Abstracts the details of WASM module loading, instantiation, and script execution.
/// </summary>
internal interface IWasmScriptExecutor
{
    /// <summary>
    /// Executes JavaScript code in the QuickJS WASM sandbox and returns the result.
    /// </summary>
    /// <param name="jsCode">The JavaScript source code to execute.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds. If null, uses the default timeout. Set to 0 for no timeout (use with caution).</param>
    /// <returns>The result of the JavaScript execution as a string and captured logs.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the WASM module cannot be located.</exception>
    /// <exception cref="InvalidOperationException">Thrown when WASM initialization or script execution fails.</exception>
    /// <exception cref="TimeoutException">Thrown when script execution exceeds the timeout limit.</exception>
    WasmExecutionResult ExecuteScript(string jsCode, int? timeoutMs = null);
}
