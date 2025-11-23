using System.Collections.Generic;

namespace ScriptBox.Core.WasmExecution;

/// <summary>
/// Represents the raw result of a WASM script execution.
/// </summary>
internal sealed class WasmExecutionResult
{
    /// <summary>
    /// The string result returned by the script (JSON or primitive string).
    /// </summary>
    public string Result { get; }

    /// <summary>
    /// The console logs captured during execution.
    /// </summary>
    public IList<string> Logs { get; }

    public WasmExecutionResult(string result, IList<string> logs)
    {
        Result = result;
        Logs = logs;
    }
}
