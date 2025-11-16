using System.Collections.Generic;
using System.Reflection;

namespace ScriptBox.SemanticKernel.Internal;

internal sealed class SemanticKernelPluginRuntimeDescriptor
{
    public SemanticKernelPluginRuntimeDescriptor(
        Type pluginType,
        string jsNamespace,
        string bootstrapCode,
        IReadOnlyList<SemanticKernelFunctionRuntimeDescriptor> functions,
        SemanticKernelNamespaceMetadata metadata,
        bool requiresInstance)
    {
        PluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
        JsNamespace = jsNamespace ?? throw new ArgumentNullException(nameof(jsNamespace));
        BootstrapCode = bootstrapCode ?? string.Empty;
        Functions = functions ?? throw new ArgumentNullException(nameof(functions));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        RequiresInstance = requiresInstance;
    }

    public Type PluginType { get; }

    public string JsNamespace { get; }

    public string BootstrapCode { get; }

    public IReadOnlyList<SemanticKernelFunctionRuntimeDescriptor> Functions { get; }

    public SemanticKernelNamespaceMetadata Metadata { get; }

    public bool RequiresInstance { get; }
}

internal sealed record SemanticKernelFunctionRuntimeDescriptor(
    MethodInfo Method,
    string HostMethodName,
    bool IsStatic);
