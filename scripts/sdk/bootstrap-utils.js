/**
 * bootstrap-utils.ts
 * Generates JavaScript proxy methods for tools discovered from the host.
 *
 * Reads scriptBoxInput.tools.descriptors metadata and creates callable proxies.
 * Routes calls back to the host via __scriptbox.callTool.
 * Aliases to globalThis.assistantApi.utils for ergonomics.
 */
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
var __generator = (this && this.__generator) || function (thisArg, body) {
    var _ = { label: 0, sent: function() { if (t[0] & 1) throw t[1]; return t[1]; }, trys: [], ops: [] }, f, y, t, g = Object.create((typeof Iterator === "function" ? Iterator : Object).prototype);
    return g.next = verb(0), g["throw"] = verb(1), g["return"] = verb(2), typeof Symbol === "function" && (g[Symbol.iterator] = function() { return this; }), g;
    function verb(n) { return function (v) { return step([n, v]); }; }
    function step(op) {
        if (f) throw new TypeError("Generator is already executing.");
        while (g && (g = 0, op[0] && (_ = 0)), _) try {
            if (f = 1, y && (t = op[0] & 2 ? y["return"] : op[0] ? y["throw"] || ((t = y["return"]) && t.call(y), 0) : y.next) && !(t = t.call(y, op[1])).done) return t;
            if (y = 0, t) op = [op[0] & 2, t.value];
            switch (op[0]) {
                case 0: case 1: t = op; break;
                case 4: _.label++; return { value: op[1], done: false };
                case 5: _.label++; y = op[1]; op = [0]; continue;
                case 7: op = _.ops.pop(); _.trys.pop(); continue;
                default:
                    if (!(t = _.trys, t = t.length > 0 && t[t.length - 1]) && (op[0] === 6 || op[0] === 2)) { _ = 0; continue; }
                    if (op[0] === 3 && (!t || (op[1] > t[0] && op[1] < t[3]))) { _.label = op[1]; break; }
                    if (op[0] === 6 && _.label < t[1]) { _.label = t[1]; t = op; break; }
                    if (t && _.label < t[2]) { _.label = t[2]; _.ops.push(op); break; }
                    if (t[2]) _.ops.pop();
                    _.trys.pop(); continue;
            }
            op = body.call(thisArg, _);
        } catch (e) { op = [6, e]; y = 0; } finally { f = t = 0; }
        if (op[0] & 5) throw op[1]; return { value: op[0] ? op[1] : void 0, done: true };
    }
};
/**
 * Initialize __scriptbox.utils namespace and populate it with tool proxies.
 */
(function initializeToolProxies(root) {
    var _a, _b;
    if (typeof __scriptbox === "undefined") {
        console.warn("[bootstrap-utils] __scriptbox not available; skipping tool initialization");
        return;
    }
    var descriptors = ((_b = (_a = scriptBoxInput === null || scriptBoxInput === void 0 ? void 0 : scriptBoxInput.tools) === null || _a === void 0 ? void 0 : _a.descriptors) !== null && _b !== void 0 ? _b : []);
    if (!Array.isArray(descriptors) || descriptors.length === 0) {
        console.debug("[bootstrap-utils] No tools to initialize");
        return;
    }
    // Ensure __scriptbox.utils exists
    if (!root.__scriptbox.utils) {
        root.__scriptbox.utils = {};
    }
    // Create proxy for each tool
    for (var _i = 0, descriptors_1 = descriptors; _i < descriptors_1.length; _i++) {
        var descriptor = descriptors_1[_i];
        var toolId = descriptor.id;
        var toolName = descriptor.name;
        if (!toolId || !toolName) {
            console.warn("[bootstrap-utils] Skipping tool with missing id or name", descriptor);
            continue;
        }
        // Create async proxy function
        var createToolProxy = function (id) {
            return function toolProxy() {
                var args = [];
                for (var _i = 0; _i < arguments.length; _i++) {
                    args[_i] = arguments[_i];
                }
                return __awaiter(this, void 0, void 0, function () {
                    var request, response, parsed, errorMsg, err_1, message;
                    var _a;
                    return __generator(this, function (_b) {
                        switch (_b.label) {
                            case 0:
                                request = {
                                    kind: "tool.invoke",
                                    toolId: id,
                                    args: args.length > 0 ? args : undefined,
                                };
                                _b.label = 1;
                            case 1:
                                _b.trys.push([1, 3, , 4]);
                                return [4 /*yield*/, __scriptbox.hostCall("tool.invoke", [JSON.stringify(request)])];
                            case 2:
                                response = _b.sent();
                                if (response === null || typeof response === "undefined") {
                                    throw new Error("Host returned null response for tool.invoke");
                                }
                                parsed = typeof response === "string" ? JSON.parse(response) : response;
                                if (parsed && typeof parsed === "object" && parsed.error) {
                                    errorMsg = parsed.error.message || "Unknown error";
                                    throw new Error("[".concat(id, "] ").concat(errorMsg));
                                }
                                return [2 /*return*/, (_a = parsed === null || parsed === void 0 ? void 0 : parsed.result) !== null && _a !== void 0 ? _a : null];
                            case 3:
                                err_1 = _b.sent();
                                message = err_1 instanceof Error ? err_1.message : String(err_1);
                                throw new Error("Failed to invoke tool ".concat(id, ": ").concat(message));
                            case 4: return [2 /*return*/];
                        }
                    });
                });
            };
        };
        root.__scriptbox.utils[toolName] = createToolProxy(toolId);
    }
    console.debug("[bootstrap-utils] Initialized ".concat(descriptors.length, " tool(s)"));
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
    var _a, _b;
    if (!((_a = root.__scriptbox) === null || _a === void 0 ? void 0 : _a.utils)) {
        console.debug("[bootstrap-utils] No utils to alias");
        return;
    }
    root.assistantApi = (_b = root.assistantApi) !== null && _b !== void 0 ? _b : {};
    root.assistantApi.utils = root.__scriptbox.utils;
    console.debug("[bootstrap-utils] Aliased assistantApi.utils");
})(typeof globalThis !== "undefined"
    ? globalThis
    : typeof global !== "undefined"
        ? global
        : typeof self !== "undefined"
            ? self
            : this);
