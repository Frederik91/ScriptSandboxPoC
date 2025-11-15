#include "quickjs.h"
#include <string.h>
#include <stdio.h>
#include <stdarg.h>

// ============================================================================
// QuickJS WASM Assistant - Error Handling and Evaluation Infrastructure
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

// ---------- Error reporting infrastructure ----------

// Global error message buffer for inter-process communication
// Accessible from host via get_last_error_ptr() and get_last_error_len()
static char g_last_error[1024];

// ---------- Global QuickJS state (legacy) ----------
// These are kept for potential future use with persistent context model

static JSRuntime* rt  = NULL;
static JSContext* ctx = NULL;
static int        initialized = 0;

static void init_vm(void)
{
    if (initialized)
        return;

    rt  = JS_NewRuntime();
    ctx = JS_NewContext(rt);
    initialized = 1;
}

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

// JS signature: __host_call_json(payload: string): string
static JSValue js_host_call_json(JSContext* ctx,
                                 JSValueConst this_val,
                                 int argc,
                                 JSValueConst* argv)
{
    if (argc < 1) {
        return JS_ThrowTypeError(ctx, "host_call_json: expected 1 argument");
    }

    size_t in_len = 0;
    const char* in_str = JS_ToCStringLen(ctx, &in_len, argv[0]);
    if (!in_str) {
        return JS_EXCEPTION;
    }

    // Fixed-size buffer for PoC. You can grow this or expose a second API
    // if you need larger payloads later.
    char out_buf[4096];
    int written = host_call(in_str, (int)in_len, out_buf, (int)sizeof(out_buf));

    JS_FreeCString(ctx, in_str);

    if (written < 0) {
        return JS_ThrowInternalError(ctx, "host_call failed with code %d", written);
    }
    if (written > (int)sizeof(out_buf)) {
        return JS_ThrowInternalError(ctx, "host_call wrote too much data");
    }

    return JS_NewStringLen(ctx, out_buf, written);
}

// Run once: define global __host_call_json
static void install_host_bridge(void)
{
    JSValue global = JS_GetGlobalObject(ctx);

    JS_SetPropertyStr(
        ctx,
        global,
        "__host_call_json",
        JS_NewCFunction(ctx, js_host_call_json, "__host_call_json", 1)
    );

    JS_FreeValue(ctx, global);
}

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

// Optional: embed a tiny bootstrap that defines assistantApi in JS.
// Absolute minimal version - just defines empty object
static const char* bootstrap_src =
    "var assistantApi = {};\n";

// Evaluate JS code in the current context
static int eval_code(const char* src, size_t len, const char* filename)
{
    JSValue result = JS_Eval(ctx, src, len, filename, JS_EVAL_TYPE_GLOBAL);

    if (JS_IsException(result)) {
        JSValue exc = JS_GetException(ctx);
        size_t elen;
        const char* estr = JS_ToCStringLen(ctx, &elen, exc);
        if (estr) {
            // Try to report error via host_call if we can, otherwise just note we had an error
            // For now just silently fail - error is bubbled up as return code
            JS_FreeCString(ctx, estr);
        }
        JS_FreeValue(ctx, exc);
        JS_FreeValue(ctx, result);
        return 1;  // Error
    }

    if (JS_IsUndefined(result) || JS_IsNull(result)) {
        // These are valid returns from eval
        JS_FreeValue(ctx, result);
        return 0;
    }

    JS_FreeValue(ctx, result);
    return 0;  // Success
}

// ---------- JavaScript Evaluation ----------

/**
 * @brief Evaluate JavaScript code in a fresh context
 * 
 * Creates a new runtime and context for each invocation, evaluates the code,
 * and cleans up resources. This ensures isolation between evaluations.
 * 
 * @param code_ptr Pointer to JavaScript source code (in WASM linear memory)
 * @param len Number of bytes to read from code_ptr
 * 
 * @return Status code:
 *   0 = Success
 *   20 = Failed to create runtime
 *   21 = Failed to create context
 *   22 = Evaluation resulted in exception (see get_last_error_ptr)
 *   23 = Global object is null/undefined
 *   24 = code_ptr is NULL
 *   25 = Failed to allocate code buffer
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

    // Verify global object is accessible
    JSValue glob = JS_GetGlobalObject(ctx);
    if (JS_IsUndefined(glob) || JS_IsNull(glob)) {
        set_error("Global object is null/undefined - context initialization failed");
        JS_FreeValue(ctx, glob);
        JS_FreeContext(ctx);
        JS_FreeRuntime(rt);
        return 23;
    }
    JS_FreeValue(ctx, glob);

    // Disable stack limit checks (QuickJS default stack limit is very conservative for WASI)
    // Setting to 0 disables the check entirely
    JS_SetMaxStackSize(rt, 0);

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

    // Evaluate the code
    JSValue result = JS_Eval(ctx, code_copy, len, "eval", JS_EVAL_TYPE_GLOBAL);

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

    // Success
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