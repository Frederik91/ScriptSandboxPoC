using System;
using System.Diagnostics.CodeAnalysis;

namespace ScriptBox.Core.Runtime;

internal sealed class AttributedSandboxApiScanner : ISandboxApiScanner
{
    public bool TryCreateDescriptor(Type type, string? namespaceOverride, [NotNullWhen(true)] out SandboxApiDescriptor? descriptor)
    {
        return AttributedSandboxApiRegistry.TryCreateDescriptor(type, namespaceOverride, out descriptor);
    }
}
