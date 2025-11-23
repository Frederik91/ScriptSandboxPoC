using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.SemanticKernel;
using ScriptBox.Core.Runtime;

namespace ScriptBox.SemanticKernel;

public class SemanticKernelApiScanner : ISandboxApiScanner
{
    public bool TryCreateDescriptor(Type type, string? namespaceOverride, [NotNullWhen(true)] out SandboxApiDescriptor? descriptor)
    {
        descriptor = null;

        string? apiName = namespaceOverride;
        if (apiName is null)
        {
            var apiAttribute = type.GetCustomAttribute<SandboxApiAttribute>();
            if (apiAttribute != null)
            {
                apiName = apiAttribute.Name;
            }
        }

        var methods = new List<SandboxMethodDescriptor>();
        var isStatic = type.IsAbstract && type.IsSealed;
        var requiresInstance = !isStatic;

        // Scan for [KernelFunction] methods
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            var kernelAttribute = method.GetCustomAttribute<KernelFunctionAttribute>();
            if (kernelAttribute is null)
            {
                continue;
            }

            if (apiName is null)
            {
                apiName = ToSnakeCase(type.Name);
            }

            var jsMethodName = !string.IsNullOrWhiteSpace(kernelAttribute.Name)
                ? kernelAttribute.Name!
                : ToSnakeCase(method.Name);

            methods.Add(new SandboxMethodDescriptor(
                apiName,
                jsMethodName,
                method));
        }

        if (methods.Count == 0)
        {
            // If no KernelFunctions found, maybe it's a pure SandboxApi? 
            // We return false so the default scanner can try (although default scanner also checks for SandboxMethod).
            // If the class has SandboxApi but no KernelFunctions, and no SandboxMethods, both scanners will fail, which is correct.
            return false;
        }

        descriptor = new SandboxApiDescriptor(type, apiName!, methods, requiresInstance);
        return true;
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var builder = new System.Text.StringBuilder(value.Length + 5);
        var previousLower = false;

        foreach (var ch in value)
        {
            if (char.IsUpper(ch))
            {
                if (previousLower)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(ch));
                previousLower = false;
            }
            else if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousLower = true;
            }
            else
            {
                builder.Append('_');
                previousLower = false;
            }
        }

        return builder.ToString().Trim('_');
    }
}
