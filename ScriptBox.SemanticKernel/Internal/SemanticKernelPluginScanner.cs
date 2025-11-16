using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using ScriptBox.Core.Runtime;

namespace ScriptBox.SemanticKernel.Internal;

internal static class SemanticKernelPluginScanner
{
    public static SemanticKernelPluginRuntimeDescriptor CreateDescriptor(
        Type pluginType,
        string jsNamespace)
    {
        if (pluginType is null)
        {
            throw new ArgumentNullException(nameof(pluginType));
        }

        var methods = new List<SemanticKernelFunctionRuntimeDescriptor>();
        var metadataFunctions = new List<SemanticKernelFunctionMetadata>();
        var requiresInstance = false;

        foreach (var method in pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            var kernelAttribute = method.GetCustomAttribute<KernelFunctionAttribute>();
            if (kernelAttribute is null)
            {
                continue;
            }

            var jsMethodName = !string.IsNullOrWhiteSpace(kernelAttribute.Name)
                ? kernelAttribute.Name!
                : ToSnakeCase(method.Name);

            var hostMethodName = $"{jsNamespace}.{jsMethodName}";
            var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description;

            var parameters = BuildParameterMetadata(method);
            var returnType = ResolveReturnType(method);
            var returnsVoid = string.Equals(returnType, "void", StringComparison.Ordinal);

            metadataFunctions.Add(new SemanticKernelFunctionMetadata(
                jsMethodName,
                description,
                parameters,
                returnsVoid ? "void" : returnType,
                returnsVoid));

            methods.Add(new SemanticKernelFunctionRuntimeDescriptor(
                method,
                hostMethodName,
                method.IsStatic));

            requiresInstance |= !method.IsStatic;
        }

        if (methods.Count == 0)
        {
            throw new InvalidOperationException(
                $"Type '{pluginType.FullName}' does not declare any public methods annotated with [KernelFunction].");
        }

        var bootstrap = SemanticKernelBootstrapBuilder.Build(jsNamespace, metadataFunctions);
        var metadata = new SemanticKernelNamespaceMetadata(jsNamespace, metadataFunctions);

        return new SemanticKernelPluginRuntimeDescriptor(
            pluginType,
            jsNamespace,
            bootstrap,
            methods,
            metadata,
            requiresInstance);
    }

    private static IReadOnlyList<SemanticKernelParameterMetadata> BuildParameterMetadata(MethodInfo method)
    {
        var metadata = new List<SemanticKernelParameterMetadata>();

        foreach (var parameter in method.GetParameters())
        {
            if (parameter.ParameterType == typeof(CancellationToken) ||
                parameter.ParameterType == typeof(HostCallContext))
            {
                continue;
            }

            var optional = parameter.HasDefaultValue || parameter.IsOptional || IsNullable(parameter.ParameterType);
            var parameterType = NormalizeType(parameter.ParameterType);
            var description = parameter.GetCustomAttribute<DescriptionAttribute>()?.Description;

            metadata.Add(new SemanticKernelParameterMetadata(
                parameter.Name ?? "arg" + metadata.Count,
                parameterType,
                optional,
                description));
        }

        return metadata;
    }

    private static bool IsNullable(Type type)
    {
        if (!type.IsValueType)
        {
            return true;
        }

        return Nullable.GetUnderlyingType(type) is not null;
    }

    private static string NormalizeType(Type type)
    {
        var inner = Nullable.GetUnderlyingType(type) ?? type;
        return SemanticKernelTypeMapper.ToTypeScriptType(inner);
    }

    private static string ResolveReturnType(MethodInfo method)
    {
        var type = method.ReturnType;
        if (type == typeof(void) || type == typeof(Task) || type == typeof(ValueTask))
        {
            return "void";
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(Task<>) || definition == typeof(ValueTask<>))
            {
                var inner = type.GetGenericArguments()[0];
                return SemanticKernelTypeMapper.ToTypeScriptType(Nullable.GetUnderlyingType(inner) ?? inner);
            }
        }

        return SemanticKernelTypeMapper.ToTypeScriptType(Nullable.GetUnderlyingType(type) ?? type);
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
