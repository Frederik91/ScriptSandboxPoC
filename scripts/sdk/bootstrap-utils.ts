/**
 * bootstrap-utils.ts
 * Generates JavaScript proxy methods for tools discovered from the host.
 * 
 * Reads scriptBoxInput.tools.descriptors metadata and creates callable proxies.
 * Routes calls back to the host via __scriptbox.callTool.
 * Aliases to globalThis.assistantApi.utils for ergonomics.
 */

// Type definitions for the sandbox environment
declare const __scriptbox: {
    hostCall: (method: string, args: unknown[]) => unknown;
    createMethod: (methodName: string) => (...args: unknown[]) => unknown;
};

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

/**
 * Initialize __scriptbox.utils namespace and populate it with tool proxies.
 */
(function initializeToolProxies(root: any) {
    if (typeof __scriptbox === "undefined") {
        console.warn("[bootstrap-utils] __scriptbox not available; skipping tool initialization");
        return;
    }

    const descriptors = (scriptBoxInput?.tools?.descriptors ?? []) as any[];
    if (!Array.isArray(descriptors) || descriptors.length === 0) {
        console.debug("[bootstrap-utils] No tools to initialize");
        return;
    }

    // Ensure __scriptbox.utils exists
    if (!root.__scriptbox.utils) {
        root.__scriptbox.utils = {};
    }

    // Create proxy for each tool
    for (const descriptor of descriptors) {
        const toolId = descriptor.id;
        const toolName = descriptor.name;

        if (!toolId || !toolName) {
            console.warn("[bootstrap-utils] Skipping tool with missing id or name", descriptor);
            continue;
        }

        // Create async proxy function
        const createToolProxy = (id: string) => {
            return async function toolProxy(...args: unknown[]): Promise<unknown> {
                const request: ToolInvocationRequest = {
                    kind: "tool.invoke",
                    toolId: id,
                    args: args.length > 0 ? args : undefined,
                };

                try {
                    const response = await __scriptbox.hostCall("tool.invoke", [JSON.stringify(request)]);
                    if (response === null || typeof response === "undefined") {
                        throw new Error("Host returned null response for tool.invoke");
                    }

                    const parsed = typeof response === "string" ? JSON.parse(response) : response;

                    if (parsed && typeof parsed === "object" && parsed.error) {
                        const errorMsg = parsed.error.message || "Unknown error";
                        throw new Error(`[${id}] ${errorMsg}`);
                    }

                    return parsed?.result ?? null;
                } catch (err) {
                    const message = err instanceof Error ? err.message : String(err);
                    throw new Error(`Failed to invoke tool ${id}: ${message}`);
                }
            };
        };

        root.__scriptbox.utils[toolName] = createToolProxy(toolId);
    }

    console.debug(`[bootstrap-utils] Initialized ${descriptors.length} tool(s)`);
})(typeof globalThis !== "undefined"
    ? globalThis
    : typeof global !== "undefined"
        ? global
        : typeof self !== "undefined"
            ? self
            : (this as any));

/**
 * Alias assistantApi.utils for ergonomic script access.
 * Scripts can now call: assistantApi.utils.method_name(...)
 */
(function aliasAssistantApi(root: any) {
    if (!root.__scriptbox?.utils) {
        console.debug("[bootstrap-utils] No utils to alias");
        return;
    }

    root.assistantApi = root.assistantApi ?? {};
    root.assistantApi.utils = root.__scriptbox.utils;

    console.debug("[bootstrap-utils] Aliased assistantApi.utils");
})(typeof globalThis !== "undefined"
    ? globalThis
    : typeof global !== "undefined"
        ? global
        : typeof self !== "undefined"
            ? self
            : (this as any));
