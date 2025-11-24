using System;
using System.Collections.Generic;
using System.Reflection;

namespace ScriptBox.Core.Runtime;

/// <summary>
/// Describes a sandbox API that has been discovered by a scanner.
/// </summary>
public sealed record SandboxApiDescriptor(
    Type ApiType,
    string JsNamespace,
    IReadOnlyList<SandboxMethodDescriptor> Methods,
    bool RequiresInstance);

/// <summary>
/// Describes a method within a sandbox API.
/// </summary>
public sealed record SandboxMethodDescriptor(string JsNamespace, string JsMethodName, MethodInfo Method)
{
    /// <summary>
    /// Gets the full host method name used for registration.
    /// </summary>
    public string HostMethodName => $"{JsNamespace}.{JsMethodName}";
}
