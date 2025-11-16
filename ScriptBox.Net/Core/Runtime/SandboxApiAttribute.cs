using System;

namespace ScriptBox.Net.Core.Runtime;

[AttributeUsage(AttributeTargets.Class)]
public sealed class SandboxApiAttribute : Attribute
{
    public SandboxApiAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("API name cannot be null or empty", nameof(name));
        }

        Name = name;
    }

    public string Name { get; }
}
