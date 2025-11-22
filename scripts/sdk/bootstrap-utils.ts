/**
 * bootstrap-utils.ts
 * Generates JavaScript proxy methods for tools discovered from the host.
 * 
 * Reads scriptBoxInput.tools.descriptors metadata and creates callable proxies.
 * Routes calls back to the host via __scriptbox.callTool.
 * Aliases to globalThis.assistantApi.utils for ergonomics.
 */

// Type definitions for the sandbox environment
// __scriptbox is declared in index.d.ts

declare var console: any;
declare var global: any;
declare var self: any;

declare const scriptBoxInput: {
    user?: unknown;
    tools?: {
        descriptors?: Array<{
            id: string;
            name: string;
            plugin?: string;
            description?: string;
            parameters?: Array<{
                name: string;
                type: string;
                isOptional: boolean;
                description?: string;
            }>;
            returnType?: string;
        }>;
    };
};

// Wire protocol for tool invocation
interface ToolInvocationRequest {
    kind: "tool.invoke";
    toolId: string;
    args?: unknown;
    callId?: string;
}

interface ToolInvocationResponse {
    result?: unknown;
    error?: {
        type: string;
        toolId: string;
        message: string;
    };
}

function createToolProxy(toolId: string) {
    if (typeof __scriptbox !== "undefined" && __scriptbox.createMethod) {
        return __scriptbox.createMethod(toolId);
    }
    return function() { throw new Error("ScriptBox not initialized"); };
}

/**
 * Initialize __scriptbox.utils namespace and populate it with tool proxies.
 * This function should be called after scriptBoxInput is defined.
 */
function initializeTools() {
    try {
        const root = typeof globalThis !== "undefined"
            ? globalThis
            : typeof global !== "undefined"
                ? global
                : typeof self !== "undefined"
                    ? self
                    : (this as any);

        if (typeof __scriptbox === "undefined") return;
        if (typeof root.scriptBoxInput === "undefined") return;

        let descriptors: any[] = [];
        if (root.scriptBoxInput && root.scriptBoxInput.tools && root.scriptBoxInput.tools.descriptors) {
            descriptors = root.scriptBoxInput.tools.descriptors;
        }
        if (!Array.isArray(descriptors) || descriptors.length === 0) return;

        if (!root.__scriptbox.utils) {
            root.__scriptbox.utils = {};
        }

        for (const descriptor of descriptors) {
            const toolId = descriptor.id;
            const toolName = descriptor.name;
            const pluginName = descriptor.plugin;

            if (!toolId || !toolName) continue;

            const proxy = createToolProxy(toolId);
            root.__scriptbox.utils[toolName] = proxy;

            if (pluginName) {
                if (!root[pluginName]) {
                    root[pluginName] = {};
                }
                root[pluginName][toolName] = proxy;
            }
        }

        // Alias assistantApi.utils
        root.assistantApi = root.assistantApi || {};
        root.assistantApi.utils = root.__scriptbox.utils;

    } catch (e) {
        // Swallow bootstrap errors
    }
}

// Expose initializeTools globally
(function(root: any) {
    if (root.__scriptbox) {
        root.__scriptbox.initializeTools = initializeTools;
    }
})(typeof globalThis !== "undefined"
    ? globalThis
    : typeof global !== "undefined"
        ? global
        : typeof self !== "undefined"
            ? self
            : (this as any));



