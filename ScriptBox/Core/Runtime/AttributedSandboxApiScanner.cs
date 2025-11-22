using System;
using System.Diagnostics.CodeAnalysis;

namespace ScriptBox.Core.Runtime;

public class AttributedSandboxApiScanner : ISandboxApiScanner
{
    public bool TryCreateDescriptor(Type type, [NotNullWhen(true)] out SandboxApiDescriptor? descriptor)
    {
        return AttributedSandboxApiRegistry.TryCreateDescriptor(type, out descriptor);
    }
}
