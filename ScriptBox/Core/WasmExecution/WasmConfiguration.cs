namespace ScriptBox.Core.WasmExecution;

/// <summary>
/// Configuration constants for WASM memory and script execution.
/// Centralized to make adjustments easier for memory tuning or different WASM layouts.
/// </summary>
public static class WasmConfiguration
{
    /// <summary>
    /// Memory offset where JavaScript source code is written.
    /// Set to 8KB to safely avoid QuickJS runtime structures (typically < 1KB).
    /// </summary>
    public const int ScriptMemoryOffset = 0x2000;

    /// <summary>
    /// Maximum allowed size for a script in WASM memory (1MB).
    /// Prevents memory exhaustion and provides a clear boundary.
    /// </summary>
    public const int MaxScriptSize = 0x100000;

    /// <summary>
    /// WASM memory export name used by QuickJS.
    /// </summary>
    public const string MemoryExportName = "memory";

    /// <summary>
    /// WASM function name for evaluating JavaScript.
    /// </summary>
    public const string EvalFunctionName = "eval_js";

    /// <summary>
    /// WASM function name for retrieving error buffer pointer.
    /// </summary>
    public const string GetErrorPtrFunctionName = "get_last_error_ptr";

    /// <summary>
    /// WASM function name for retrieving error buffer length.
    /// </summary>
    public const string GetErrorLenFunctionName = "get_last_error_len";

    /// <summary>
    /// Success status code returned by eval_js.
    /// </summary>
    public const int SuccessStatusCode = 0;

    /// <summary>
    /// Default timeout for script execution in milliseconds.
    /// Scripts exceeding this time will be terminated.
    /// </summary>
    public const int DefaultTimeoutMs = 5000; // 5 seconds

    /// <summary>
    /// Fuel units consumed per instruction (approximate).
    /// Wasmtime uses fuel to limit execution - higher values = shorter timeouts.
    /// Calibrated based on typical QuickJS instruction execution rates.
    /// </summary>
    public const long FuelPerMs = 100000; // Approximately 100K fuel units per millisecond
}
