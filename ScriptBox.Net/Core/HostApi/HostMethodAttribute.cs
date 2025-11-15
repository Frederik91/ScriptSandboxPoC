namespace ScriptBox.Net.Core.HostApi;

/// <summary>
/// Marks a method as callable from JavaScript in the WASM sandbox.
/// Methods with this attribute can be automatically registered and invoked.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class HostMethodAttribute : Attribute
{
    /// <summary>
    /// The name of the method as it will be called from JavaScript.
    /// If not specified, uses the C# method name.
    /// </summary>
    public string? MethodName { get; }

    /// <summary>
    /// Optional description of what this method does (for documentation).
    /// </summary>
    public string? Description { get; set; }

    public HostMethodAttribute(string? methodName = null)
    {
        MethodName = methodName;
    }
}
