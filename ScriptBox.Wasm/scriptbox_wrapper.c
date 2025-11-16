#include "quickjs.h"
#include <string.h>
#include <stdio.h>
#include <stdarg.h>

// ============================================================================
// QuickJS WASM ScriptBox Bridge - Error Handling and Evaluation Infrastructure
// ============================================================================
//
// This module provides:
// - Error message capture and reporting
// - Safe JavaScript evaluation with buffer management
// - Diagnostic functions for troubleshooting
//
// ============================================================================

// ---------- Host import ----------

__attribute__((import_module("host"), import_name("call")))
int host_call(const char* in_ptr, int in_len,
              char* out_ptr, int out_cap);

__attribute__((import_module("host"), import_name("log")))
void host_log(const char* ptr, int len);

// ---------- Error reporting infrastructure ----------

// Global error message buffer for inter-process communication
// Accessible from host via get_last_error_ptr() and get_last_error_len()
static char g_last_error[1024];

// Global result buffer for returning JavaScript values to the host
// Accessible from host via get_result_ptr() and get_result_len()
static char g_result[65536];  // 64KB buffer for result values

// ---------- Global QuickJS state ----------
// Note: Each eval_js call creates its own runtime/context for isolation

// ---------- Error message handling ----------
//
// The error buffer is used to communicate detailed error information back to
// the host process after evaluation fails. This is critical for debugging.

/**
 * @brief Set error message in global buffer
 * @param fmt Format string (printf-style)
 */
static void set_error(const char* fmt, ...) {
    va_list args;
    va_start(args, fmt);
    vsnprintf(g_last_error, sizeof(g_last_error), fmt, args);
    va_end(args);
}

/**
 * @brief Extract and format a QuickJS exception into the error buffer
 * @param ctx The QuickJS context
 * @param exc The exception value from JS_GetException()
 */
static void capture_exception(JSContext* ctx, JSValue exc) {
    const char* msg = JS_ToCString(ctx, exc);
    
    if (msg) {
        set_error("Exception: %s", msg);
        JS_FreeCString(ctx, msg);
    } else {
        // Exception object (not directly stringifiable) - try to extract message property
        JSValue msgProp = JS_GetPropertyStr(ctx, exc, "message");
        const char* msgStr = JS_ToCString(ctx, msgProp);
        
        if (msgStr) {
            set_error("Exception: %s", msgStr);
            JS_FreeCString(ctx, msgStr);
        } else {
            set_error("Exception: (unable to extract message)");
        }
        JS_FreeValue(ctx, msgProp);
    }
    
    // Append stack trace if available
    JSValue stackVal = JS_GetPropertyStr(ctx, exc, "stack");
    if (!JS_IsUndefined(stackVal)) {
        const char* stackStr = JS_ToCString(ctx, stackVal);
        if (stackStr) {
            size_t cur = strlen(g_last_error);
            snprintf(g_last_error + cur, sizeof(g_last_error) - cur,
                     "\nStack: %s", stackStr);
            JS_FreeCString(ctx, stackStr);
        }
    }
    JS_FreeValue(ctx, stackVal);
}

// ---------- JS <-> host_call bridge ----------

// JS signature: __host.bridge(payload: string): string | null
// This function bridges JavaScript calls to the host via the WASM import.
// It accepts a JSON string, forwards it to the host, and returns the host's response.
static JSValue js_bridge_call(JSContext *ctx, JSValueConst this_val,
                               int argc, JSValueConst *argv)
{
    // Validate argument count
    if (argc < 1) {
        return JS_ThrowTypeError(ctx, "bridge requires 1 argument (JSON string)");
    }
    
    // Extract the JSON payload from the first argument
    size_t payload_len;
    const char* payload = JS_ToCStringLen(ctx, &payload_len, argv[0]);
    if (!payload) {
        return JS_ThrowTypeError(ctx, "bridge argument must be a string");
    }
    
    // Prepare output buffer for host response
    // Using a reasonable fixed size; could be made dynamic if needed
    char response_buf[4096];
    
    // Call the host via WASM import
    // host_call(input_ptr, input_len, output_ptr, output_capacity)
    int response_len = host_call(payload, (int)payload_len, response_buf, sizeof(response_buf));
    
    // Clean up the input string
    JS_FreeCString(ctx, payload);
    
    // Handle host call errors
    if (response_len < 0) {
        return JS_ThrowInternalError(ctx, "Host call failed with error code %d", response_len);
    }
    
    if (response_len == 0) {
        // Host returned empty response - return null
        return JS_NULL;
    }
    
    // Return the host's response as a JavaScript string
    return JS_NewStringLen(ctx, response_buf, response_len);
}

// Note: Host bridge is installed per-evaluation in eval_js()

// ---------- Error reporting ----------

/**
 * @brief Get pointer to error message buffer
 * @return Pointer to null-terminated error string (valid until next eval_js call)
 * 
 * The returned pointer points to WASM linear memory and is valid for the
 * lifetime of the current WASM instance. Use get_last_error_len() to determine
 * the length before copying.
 */
__attribute__((export_name("get_last_error_ptr")))
const char* get_last_error_ptr(void) {
    return g_last_error;
}

/**
 * @brief Get length of error message
 * @return Length in bytes (not including null terminator)
 */
__attribute__((export_name("get_last_error_len")))
int get_last_error_len(void) {
    return (int)strlen(g_last_error);
}

/**
 * @brief Get pointer to result buffer
 * @return Pointer to null-terminated result string (valid until next eval_js call)
 *
 * The returned pointer points to WASM linear memory and is valid for the
 * lifetime of the current WASM instance. Use get_result_len() to determine
 * the length before copying.
 */
__attribute__((export_name("get_result_ptr")))
const char* get_result_ptr(void) {
    return g_result;
}

/**
 * @brief Get length of result
 * @return Length in bytes (not including null terminator)
 */
__attribute__((export_name("get_result_len")))
int get_result_len(void) {
    return (int)strlen(g_result);
}

// ---------- Console logging ----------

static JSValue js_console_log(JSContext *ctx, JSValueConst this_val,
                              int argc, JSValueConst *argv)
{
    size_t len = 0;
    const char* str = JS_ToCStringLen(ctx, &len, argv[0]);
    if (!str)
        return JS_EXCEPTION;

    host_log(str, (int)len);
    JS_FreeCString(ctx, str);
    return JS_UNDEFINED;
}

// ---------- Host bridge installation ----------

// Install host bridge (console.log, __host.bridge) into the JS context.
// This provides the minimal, stable bridge primitives.
// Higher-level APIs (scriptbox, etc.) are injected as JS by the host.
static int install_host_bridge(JSContext* ctx) {
    JSValue global = JS_GetGlobalObject(ctx);
    if (JS_IsUndefined(global) || JS_IsNull(global)) {
        JS_FreeValue(ctx, global);
        set_error("Global object is null/undefined - context initialization failed");
        return -1;
    }

    // -------- Install console.log --------
    JSValue console = JS_NewObject(ctx);
    if (JS_IsException(console)) {
        JS_FreeValue(ctx, global);
        set_error("Failed to create console object");
        return -1;
    }

    JSValue logFn = JS_NewCFunction(ctx, js_console_log, "log", 1);
    if (JS_IsException(logFn)) {
        JS_FreeValue(ctx, console);
        JS_FreeValue(ctx, global);
        set_error("Failed to create console.log function");
        return -1;
    }
    JS_SetPropertyStr(ctx, console, "log", logFn);
    JS_SetPropertyStr(ctx, global, "console", console);

    // -------- Install __host object with functions --------
    JSValue hostObj = JS_NewObject(ctx);
    if (JS_IsException(hostObj)) {
        JS_FreeValue(ctx, global);
        set_error("Failed to create __host object");
        return -1;
    }
    
    // Register bridge function for host communication
    JSValue bridgeFn = JS_NewCFunction(ctx, js_bridge_call, "bridge", 1);
    if (JS_IsException(bridgeFn)) {
        JS_FreeValue(ctx, hostObj);
        JS_FreeValue(ctx, global);
        set_error("Failed to create bridge function");
        return -1;
    }
    JS_SetPropertyStr(ctx, hostObj, "bridge", bridgeFn);
    
    // Attach __host to global
    JS_SetPropertyStr(ctx, global, "__host", hostObj);
    JS_FreeValue(ctx, global);
    
    return 0;
}

// ---------- Result Conversion ----------

/**
 * @brief Convert a JavaScript value to a string representation
 *
 * For primitives (number, boolean, string, null, undefined): uses ToString
 * For objects and arrays: uses JSON.stringify
 *
 * @param ctx The QuickJS context
 * @param val The JavaScript value to convert
 * @param result_buf Output buffer for the result
 * @param result_buf_size Size of the output buffer
 * @return 0 on success, -1 on error
 */
static int js_value_to_string(JSContext* ctx, JSValue val, char* result_buf, size_t result_buf_size) {
    // Clear result buffer
    result_buf[0] = '\0';

    // Handle different value types
    if (JS_IsUndefined(val)) {
        snprintf(result_buf, result_buf_size, "undefined");
        return 0;
    }

    if (JS_IsNull(val)) {
        snprintf(result_buf, result_buf_size, "null");
        return 0;
    }

    if (JS_IsBool(val)) {
        int bval = JS_ToBool(ctx, val);
        snprintf(result_buf, result_buf_size, "%s", bval ? "true" : "false");
        return 0;
    }

    if (JS_IsNumber(val) || JS_IsString(val)) {
        // For numbers and strings, use direct ToString
        const char* str = JS_ToCString(ctx, val);
        if (!str) {
            set_error("Failed to convert value to string");
            return -1;
        }
        snprintf(result_buf, result_buf_size, "%s", str);
        JS_FreeCString(ctx, str);
        return 0;
    }

    // For objects and arrays, use JSON.stringify
    if (JS_IsObject(val)) {
        // Get JSON global object
        JSValue global = JS_GetGlobalObject(ctx);
        JSValue json_obj = JS_GetPropertyStr(ctx, global, "JSON");
        JSValue stringify_fn = JS_GetPropertyStr(ctx, json_obj, "stringify");

        // Call JSON.stringify(val)
        JSValue args[1] = { val };
        JSValue json_result = JS_Call(ctx, stringify_fn, json_obj, 1, args);

        if (JS_IsException(json_result)) {
            // JSON.stringify failed - try toString as fallback
            JS_FreeValue(ctx, json_result);
            JS_FreeValue(ctx, stringify_fn);
            JS_FreeValue(ctx, json_obj);
            JS_FreeValue(ctx, global);

            const char* str = JS_ToCString(ctx, val);
            if (!str) {
                set_error("Failed to convert object to string");
                return -1;
            }
            snprintf(result_buf, result_buf_size, "%s", str);
            JS_FreeCString(ctx, str);
            return 0;
        }

        const char* json_str = JS_ToCString(ctx, json_result);
        if (!json_str) {
            JS_FreeValue(ctx, json_result);
            JS_FreeValue(ctx, stringify_fn);
            JS_FreeValue(ctx, json_obj);
            JS_FreeValue(ctx, global);
            set_error("Failed to convert JSON result to string");
            return -1;
        }

        snprintf(result_buf, result_buf_size, "%s", json_str);

        JS_FreeCString(ctx, json_str);
        JS_FreeValue(ctx, json_result);
        JS_FreeValue(ctx, stringify_fn);
        JS_FreeValue(ctx, json_obj);
        JS_FreeValue(ctx, global);
        return 0;
    }

    // Fallback: try to convert to string
    const char* str = JS_ToCString(ctx, val);
    if (!str) {
        set_error("Failed to convert value to string");
        return -1;
    }
    snprintf(result_buf, result_buf_size, "%s", str);
    JS_FreeCString(ctx, str);
    return 0;
}

// ---------- JavaScript Evaluation ----------

/**
 * @brief Evaluate JavaScript code in a fresh context
 *
 * Creates a new runtime and context for each invocation, evaluates the code,
 * captures the return value, and cleans up resources. This ensures isolation
 * between evaluations.
 *
 * @param code_ptr Pointer to JavaScript source code (in WASM linear memory)
 * @param len Number of bytes to read from code_ptr
 *
 * @return Status code:
 *   0 = Success (result available via get_result_ptr/get_result_len)
 *   20 = Failed to create runtime
 *   21 = Failed to create context
 *   22 = Evaluation resulted in exception (see get_last_error_ptr)
 *   23 = Global object is null/undefined
 *   24 = code_ptr is NULL
 *   25 = Failed to allocate code buffer
 *   26 = Failed to convert result to string
 *
 * On success, call get_result_ptr() and get_result_len() to retrieve the
 * JavaScript return value as a string. Primitives are converted to their
 * string representation, objects and arrays are converted to JSON.
 *
 * On error, call get_last_error_ptr() and get_last_error_len() to retrieve
 * a human-readable error message including exception details and stack trace.
 */
__attribute__((export_name("eval_js")))
int eval_js(const char* code_ptr, int len)
{
    // Validate input
    if (code_ptr == NULL) {
        set_error("code_ptr is NULL");
        return 24;
    }
    
    // Create fresh runtime and context for this evaluation
    JSRuntime* rt = JS_NewRuntime();
    if (!rt) {
        set_error("Failed to create JavaScript runtime");
        return 20;
    }

    JSContext* ctx = JS_NewContext(rt);
    if (!ctx) {
        set_error("Failed to create JavaScript context");
        JS_FreeRuntime(rt);
        return 21;
    }

    // Disable stack limit checks for WASI
    // Setting to 0 disables the check entirely (QuickJS default is very conservative for WASI)
    JS_SetMaxStackSize(rt, 0);

    // Install console.log and __host_call_json
    if (install_host_bridge(ctx) != 0) {
        // install_host_bridge already set an error message
        JS_FreeContext(ctx);
        JS_FreeRuntime(rt);
        return 23;
    }

    // Create a null-terminated copy of the code buffer
    // This is necessary because JS_Eval may read past the boundary in certain edge cases
    char* code_copy = js_malloc_rt(rt, len + 1);
    if (!code_copy) {
        set_error("Failed to allocate code buffer (%d bytes)", len + 1);
        JS_FreeContext(ctx);
        JS_FreeRuntime(rt);
        return 25;
    }
    
    // Copy code and ensure null termination
    for (int i = 0; i < len; i++) {
        code_copy[i] = code_ptr[i];
    }
    code_copy[len] = '\0';

    // Evaluate the code in the existing global scope (no flags = use current context's global)
    // Note: JS_EVAL_TYPE_GLOBAL creates a NEW global scope, which would lose our bridge functions!
    JSValue result = JS_Eval(ctx, code_copy, len, "eval", 0);

    // Handle evaluation result
    if (JS_IsException(result)) {
        JSValue exc = JS_GetException(ctx);
        capture_exception(ctx, exc);
        JS_FreeValue(ctx, exc);
        JS_FreeValue(ctx, result);
        js_free_rt(rt, code_copy);
        JS_FreeContext(ctx);
        JS_FreeRuntime(rt);
        return 22;
    }

    // Success - capture the result value
    if (js_value_to_string(ctx, result, g_result, sizeof(g_result)) != 0) {
        // Failed to convert result to string
        JS_FreeValue(ctx, result);
        js_free_rt(rt, code_copy);
        JS_FreeContext(ctx);
        JS_FreeRuntime(rt);
        return 26;  // New error code for result conversion failure
    }

    JS_FreeValue(ctx, result);
    js_free_rt(rt, code_copy);
    JS_FreeContext(ctx);
    JS_FreeRuntime(rt);
    set_error("OK");
    return 0;
}

// ---------- Diagnostic Functions ----------

/**
 * @brief Minimal QuickJS self-test
 * 
 * Runs a simple expression evaluation (1+1) to verify that QuickJS is properly
 * initialized and that the evaluation pipeline works. This is useful for
 * diagnosing build or environment issues.
 * 
 * @return Status code:
 *   0 = Success (QuickJS is functional)
 *   100 = Failed to create runtime
 *   101 = Failed to create context
 *   102 = Failed to evaluate test expression
 * 
 * The result or error message is available via get_last_error_ptr().
 */
__attribute__((export_name("quickjs_selftest")))
int quickjs_selftest(void) {
    JSRuntime* rt = JS_NewRuntime();
    if (!rt) {
        set_error("Selftest: Failed to create runtime");
        return 100;
    }

    JSContext* ctx = JS_NewContext(rt);
    if (!ctx) {
        set_error("Selftest: Failed to create context");
        JS_FreeRuntime(rt);
        return 101;
    }

    // Disable stack checks for testing
    JS_SetMaxStackSize(rt, 0);

    // Simple test: evaluate "1+1"
    const char* src = "1+1";
    JSValue result = JS_Eval(ctx, src, 3, "selftest", JS_EVAL_TYPE_GLOBAL);

    if (JS_IsException(result)) {
        JSValue exc = JS_GetException(ctx);
        capture_exception(ctx, exc);
        JS_FreeValue(ctx, exc);
        JS_FreeValue(ctx, result);
        JS_FreeContext(ctx);
        JS_FreeRuntime(rt);
        return 102;
    }

    JS_FreeValue(ctx, result);
    JS_FreeContext(ctx);
    JS_FreeRuntime(rt);
    set_error("Selftest: OK (QuickJS is functional)");
    return 0;
}
