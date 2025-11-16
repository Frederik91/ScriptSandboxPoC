/**
 * bootstrap-utils.js
 * Generates JavaScript proxy methods for tools discovered from the host.
 * 
 * Reads scriptBoxInput.tools.descriptors metadata and creates callable proxies.
 * Routes calls back to the host via __scriptbox.hostCall.
 * Aliases to globalThis.assistantApi.utils for ergonomics.
 */

(function initializeToolProxies(root) {
    if (typeof __scriptbox === "undefined") {
        console.warn("[bootstrap-utils] __scriptbox not available; skipping tool initialization");
        return;
    }

    var descriptors = (scriptBoxInput && scriptBoxInput.tools && scriptBoxInput.tools.descriptors) || [];
    if (!Array.isArray(descriptors) || descriptors.length === 0) {
        console.debug("[bootstrap-utils] No tools to initialize");
        return;
    }

    // Ensure __scriptbox.utils exists
    if (!root.__scriptbox.utils) {
        root.__scriptbox.utils = {};
    }

    // Create proxy for each tool
    for (var i = 0; i < descriptors.length; i++) {
        var descriptor = descriptors[i];
        var toolId = descriptor.id;
        var toolName = descriptor.name;

        if (!toolId || !toolName) {
            console.warn("[bootstrap-utils] Skipping tool with missing id or name", descriptor);
            continue;
        }

        // Create async proxy function (using IIFE to capture toolId correctly)
        (function(id, name) {
            root.__scriptbox.utils[name] = function toolProxy() {
                var args = Array.prototype.slice.call(arguments);
                var request = {
                    kind: "tool.invoke",
                    toolId: id,
                    args: args.length > 0 ? args : undefined
                };

                try {
                    var response = __scriptbox.hostCall("tool.invoke", [JSON.stringify(request)]);
                    if (response === null || typeof response === "undefined") {
                        throw new Error("Host returned null response for tool.invoke");
                    }

                    var parsed = typeof response === "string" ? JSON.parse(response) : response;

                    if (parsed && typeof parsed === "object" && parsed.error) {
                        var errorMsg = parsed.error.message || "Unknown error";
                        throw new Error("[" + id + "] " + errorMsg);
                    }

                    return parsed ? parsed.result : null;
                } catch (err) {
                    var message = err instanceof Error ? err.message : String(err);
                    throw new Error("Failed to invoke tool " + id + ": " + message);
                }
            };
        })(toolId, toolName);
    }

    console.debug("[bootstrap-utils] Initialized " + descriptors.length + " tool(s)");
})(typeof globalThis !== "undefined"
    ? globalThis
    : typeof global !== "undefined"
        ? global
        : typeof self !== "undefined"
            ? self
            : this);

/**
 * Alias assistantApi.utils for ergonomic script access.
 * Scripts can now call: assistantApi.utils.method_name(...)
 */
(function aliasAssistantApi(root) {
    if (!root.__scriptbox || !root.__scriptbox.utils) {
        console.debug("[bootstrap-utils] No utils to alias");
        return;
    }

    root.assistantApi = root.assistantApi || {};
    root.assistantApi.utils = root.__scriptbox.utils;

    console.debug("[bootstrap-utils] Aliased assistantApi.utils");
})(typeof globalThis !== "undefined"
    ? globalThis
    : typeof global !== "undefined"
        ? global
        : typeof self !== "undefined"
            ? self
            : this);
