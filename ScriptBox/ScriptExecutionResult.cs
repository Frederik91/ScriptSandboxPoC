using System.Collections.Generic;

namespace ScriptBox;

/// <summary>
/// Represents the result of a script execution, including the return value and any console logs.
/// </summary>
public class ScriptExecutionResult
{
    /// <summary>
    /// The return value of the script.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// The console logs captured during execution.
    /// </summary>
    public IList<string> Logs { get; set; } = new List<string>();
}
