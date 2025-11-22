# ScriptBox Troubleshooting Log

This document maintains a record of technical challenges encountered during the development of ScriptBox and their solutions. It serves as a knowledge base for future debugging and development.

## 2025-11-22: WASM Memory Corruption (Status 22 Trap)

### Symptom
When running integration tests (specifically `ScriptBox.SemanticKernel.Tests`), the WASM runtime would crash with a `Status 22` (WASM Trap) immediately after the host successfully processed a call and returned a value. The error message reported by `eval_js` was sometimes corrupted, displaying fragments of the source code instead of an error string.

### Root Cause
The issue was caused by unsafe memory writes when injecting the JavaScript source code into the WASM instance.
The `WasmScriptExecutor` was writing the script to a hardcoded memory offset (`0x2000`). This offset likely conflicted with memory regions used by the C wrapper's stack, the QuickJS runtime, or other static data, leading to memory corruption. When the script size grew (e.g., by adding bootstrap code), the likelihood of overwriting critical data increased.

### Solution
We moved away from hardcoded memory offsets for dynamic data.

1.  **WASM Side (`scriptbox_wrapper.c`)**:
    *   Allocated a dedicated, static 1MB buffer (`g_script_buffer`) specifically for receiving script content.
    *   Exported two new functions: `get_script_buffer_ptr()` and `get_script_buffer_len()` to expose the buffer's location and size to the host.

2.  **Host Side (`WasmScriptExecutor.cs`)**:
    *   Updated `WriteStringToMemory` to dynamically query the WASM module for the script buffer's location using the new exports.
    *   Added fallback logic to support older WASM modules (using the old `0x2000` offset) for backward compatibility, though the new module is recommended.

### Key Takeaway
Avoid hardcoded memory offsets when sharing memory between the Host and WASM. Always let the WASM module define and export the location of buffers to ensure memory safety and prevent collisions with the runtime's internal memory layout.
