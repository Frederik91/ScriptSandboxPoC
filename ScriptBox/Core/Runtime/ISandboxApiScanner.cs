using System;
using System.Diagnostics.CodeAnalysis;

namespace ScriptBox.Core.Runtime;

internal interface ISandboxApiScanner
{
    bool TryCreateDescriptor(Type type, string? namespaceOverride, [NotNullWhen(true)] out SandboxApiDescriptor? descriptor);
}
