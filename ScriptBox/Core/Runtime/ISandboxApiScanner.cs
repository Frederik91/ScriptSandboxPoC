using System;
using System.Diagnostics.CodeAnalysis;

namespace ScriptBox.Core.Runtime;

/// <summary>
/// Defines a scanner that can process types and create API descriptors for ScriptBox.
/// Implement this interface to add support for custom attribute-based APIs.
/// </summary>
public interface ISandboxApiScanner
{
    /// <summary>
    /// Attempts to create a sandbox API descriptor from the given type.
    /// </summary>
    /// <param name="type">The type to scan for API methods.</param>
    /// <param name="namespaceOverride">Optional namespace override for the API.</param>
    /// <param name="descriptor">The created descriptor if successful.</param>
    /// <returns>True if the scanner can handle this type and created a descriptor.</returns>
    bool TryCreateDescriptor(Type type, string? namespaceOverride, [NotNullWhen(true)] out SandboxApiDescriptor? descriptor);
}
