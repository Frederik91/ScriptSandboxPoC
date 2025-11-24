using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.SemanticKernel;
using ScriptBox.Core.Runtime;
using ScriptBox.SemanticKernel.Internal;

namespace ScriptBox.SemanticKernel;

internal sealed class SemanticKernelApiScanner : ISandboxApiScanner
{
    private readonly Action<SemanticKernelNamespaceMetadata>? _metadataCallback;

    public SemanticKernelApiScanner(Action<SemanticKernelNamespaceMetadata>? metadataCallback = null)
    {
        _metadataCallback = metadataCallback;
    }

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
        var metadataFunctions = new List<SemanticKernelFunctionMetadata>();
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

            // Also collect metadata for TypeScript generation
            metadataFunctions.Add(CreateFunctionMetadata(method, jsMethodName, kernelAttribute));
        }

        if (methods.Count == 0)
        {
            // If no KernelFunctions found, maybe it's a pure SandboxApi?
            // We return false so the default scanner can try (although default scanner also checks for SandboxMethod).
            // If the class has SandboxApi but no KernelFunctions, and no SandboxMethods, both scanners will fail, which is correct.
            return false;
        }

        descriptor = new SandboxApiDescriptor(type, apiName!, methods, requiresInstance);

        // Store metadata if callback is provided
        if (_metadataCallback != null && metadataFunctions.Count > 0)
        {
            var metadata = new SemanticKernelNamespaceMetadata(apiName!, metadataFunctions);
            _metadataCallback(metadata);
        }

        return true;
    }

    private static SemanticKernelFunctionMetadata CreateFunctionMetadata(
        MethodInfo method,
        string jsMethodName,
        KernelFunctionAttribute kernelAttribute)
    {
        var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
        var parameters = new List<SemanticKernelParameterMetadata>();

        foreach (var param in method.GetParameters())
        {
            // Skip special parameters
            if (param.ParameterType == typeof(System.Threading.CancellationToken) ||
                param.ParameterType == typeof(ScriptBox.Core.Runtime.HostCallContext))
            {
                continue;
            }

            var paramDescription = param.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
            var isOptional = param.IsOptional || param.HasDefaultValue;
            var tsType = MapToTypeScriptType(param.ParameterType);

            parameters.Add(new SemanticKernelParameterMetadata(
                param.Name ?? "arg",
                tsType,
                paramDescription,
                isOptional));
        }

        var returnType = method.ReturnType;
        var isAsync = returnType.IsGenericType &&
                     (returnType.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>) ||
                      returnType.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.ValueTask<>));

        var actualReturnType = isAsync
            ? returnType.GetGenericArguments()[0]
            : returnType == typeof(System.Threading.Tasks.Task) || returnType == typeof(System.Threading.Tasks.ValueTask)
                ? typeof(void)
                : returnType;

        var returnsVoid = actualReturnType == typeof(void);
        var tsReturnType = returnsVoid ? "void" : MapToTypeScriptType(actualReturnType);

        return new SemanticKernelFunctionMetadata(
            jsMethodName,
            tsReturnType,
            description,
            parameters,
            returnsVoid);
    }

    private static string MapToTypeScriptType(Type dotnetType)
    {
        if (dotnetType == typeof(string))
            return "string";
        if (dotnetType == typeof(int) || dotnetType == typeof(long) ||
            dotnetType == typeof(double) || dotnetType == typeof(float) ||
            dotnetType == typeof(decimal) || dotnetType == typeof(short) ||
            dotnetType == typeof(byte) || dotnetType == typeof(uint) ||
            dotnetType == typeof(ulong) || dotnetType == typeof(ushort))
            return "number";
        if (dotnetType == typeof(bool))
            return "boolean";
        if (dotnetType.IsArray || (dotnetType.IsGenericType &&
            dotnetType.GetGenericTypeDefinition() == typeof(List<>)))
            return "any[]";
        if (dotnetType == typeof(object))
            return "any";

        return "any";
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
