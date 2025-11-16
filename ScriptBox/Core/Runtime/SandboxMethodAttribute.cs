using System;

namespace ScriptBox.Core.Runtime;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SandboxMethodAttribute : Attribute
{
    public SandboxMethodAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Method name cannot be null or empty", nameof(name));
        }

        Name = name;
    }

    public string Name { get; }
}
