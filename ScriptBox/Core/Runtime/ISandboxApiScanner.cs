using System;
using System.Diagnostics.CodeAnalysis;

namespace ScriptBox.Core.Runtime;

public interface ISandboxApiScanner
{
    bool TryCreateDescriptor(Type type, [NotNullWhen(true)] out SandboxApiDescriptor? descriptor);
}
