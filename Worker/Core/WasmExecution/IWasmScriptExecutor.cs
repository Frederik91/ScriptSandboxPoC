namespace Worker.Core.WasmExecution;

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
    /// <exception cref="FileNotFoundException">Thrown when the WASM module cannot be located.</exception>
    /// <exception cref="InvalidOperationException">Thrown when WASM initialization or script execution fails.</exception>
    void ExecuteScript(string jsCode);
}
