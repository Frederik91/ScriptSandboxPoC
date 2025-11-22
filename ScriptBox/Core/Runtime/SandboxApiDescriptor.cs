using System;
using System.Collections.Generic;
using System.Reflection;

namespace ScriptBox.Core.Runtime;

public sealed record SandboxApiDescriptor(
    Type ApiType,
    string JsNamespace,
    IReadOnlyList<SandboxMethodDescriptor> Methods,
    bool RequiresInstance);

public sealed record SandboxMethodDescriptor(string JsNamespace, string JsMethodName, MethodInfo Method)
{
    public string HostMethodName => $"{JsNamespace}.{JsMethodName}";
}
