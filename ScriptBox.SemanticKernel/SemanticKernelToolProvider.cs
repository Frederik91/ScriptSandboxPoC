using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using ScriptBox.SemanticKernel.Internal;

namespace ScriptBox.SemanticKernel;

/// <summary>
/// Provides tools from registered Semantic Kernel plugins.
/// Discovers [KernelFunction] methods and exposes them as ScriptBoxToolDescriptor.
/// Invokes tools by routing through the Kernel.
/// </summary>
public sealed class SemanticKernelToolProvider : IScriptBoxToolProvider
{
    private readonly Kernel _kernel;
    private IReadOnlyList<ScriptBoxToolDescriptor>? _toolsCache;
    private readonly Dictionary<string, (string pluginName, string functionName, MethodInfo method, object? instance)> _toolRegistry = new();

    public SemanticKernelToolProvider(Kernel kernel)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    /// <summary>
    /// Discovers all tools from registered Semantic Kernel plugins.
    /// </summary>
    public IReadOnlyList<ScriptBoxToolDescriptor> GetTools()
    {
        if (_toolsCache != null)
        {
            return _toolsCache;
        }

        var descriptors = new List<ScriptBoxToolDescriptor>();

        foreach (var plugin in _kernel.Plugins)
        {
            var pluginName = plugin.Name;

            foreach (var function in plugin.Where(f => f is not null))
            {
                var metadata = function.Metadata;
                var functionName = metadata.Name;
                var jsMethodName = ToJavaScriptSafeName(functionName);
                var toolId = $"{pluginName}.{jsMethodName}";

                var parameters = BuildParameterDescriptors(metadata);
                var returnType = metadata.ReturnParameter?.Description ?? "any";

                var descriptor = new ScriptBoxToolDescriptor(
                    Id: toolId,
                    Name: jsMethodName,
                    Plugin: pluginName,
                    Description: metadata.Description,
                    Parameters: parameters,
                    ReturnType: returnType);

                descriptors.Add(descriptor);

                // Register for invocation lookup
                _toolRegistry[toolId] = (pluginName, functionName, method: null!, instance: null);
            }
        }

        _toolsCache = descriptors.AsReadOnly();
        return _toolsCache;
    }

    /// <summary>
    /// Invokes a tool by its ID.
    /// </summary>
    public async Task<object?> InvokeToolAsync(string toolId, object? args, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            throw new ArgumentException("Tool ID cannot be null or empty.", nameof(toolId));
        }

        if (!_toolRegistry.TryGetValue(toolId, out var registration))
        {
            throw new InvalidOperationException($"Tool not found: {toolId}");
        }

        var (pluginName, functionName, _, _) = registration;

        try
        {
            var plugin = _kernel.Plugins.FirstOrDefault(p => p.Name == pluginName);
            if (plugin is null)
            {
                throw new InvalidOperationException($"Plugin not found: {pluginName}");
            }

            var function = plugin.FirstOrDefault(f => f?.Metadata.Name == functionName);
            if (function is null)
            {
                throw new InvalidOperationException($"Function not found: {pluginName}.{functionName}");
            }

            // Convert args array to KernelArguments
            var kernelArgs = ConvertArgsToKernelArguments(function, args);

            // Invoke the function
            var result = await _kernel.InvokeAsync(function, kernelArgs, cancellationToken).ConfigureAwait(false);

            // Unwrap result
            return UnwrapResult(result);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error invoking tool {toolId}: {ex.Message}", ex);
        }
    }

    private static IReadOnlyList<ScriptBoxParameterDescriptor> BuildParameterDescriptors(KernelFunctionMetadata metadata)
    {
        var descriptors = new List<ScriptBoxParameterDescriptor>();

        foreach (var param in metadata.Parameters)
        {
            descriptors.Add(new ScriptBoxParameterDescriptor(
                Name: param.Name,
                Type: param.Description ?? "any",
                IsOptional: !param.IsRequired,
                Description: param.Description));
        }

        return descriptors;
    }

    private static KernelArguments ConvertArgsToKernelArguments(KernelFunction function, object? args)
    {
        var kernelArgs = new KernelArguments();

        if (args is null)
        {
            return kernelArgs;
        }

        var parameters = function.Metadata.Parameters.ToList();

        // If args is an array, map by position
        if (args is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                if (index >= parameters.Count)
                {
                    break;
                }

                var paramName = parameters[index].Name;
                kernelArgs[paramName] = item.GetRawText();
                index++;
            }
        }
        else if (args is System.Collections.IEnumerable enumerable && !(args is string))
        {
            // Handle array-like objects
            var index = 0;
            foreach (var item in enumerable)
            {
                if (index >= parameters.Count)
                {
                    break;
                }

                var paramName = parameters[index].Name;
                kernelArgs[paramName] = item;
                index++;
            }
        }
        else if (args is JsonElement objectElement && objectElement.ValueKind == JsonValueKind.Object)
        {
            // Handle object with named properties
            foreach (var property in objectElement.EnumerateObject())
            {
                kernelArgs[property.Name] = property.Value.GetRawText();
            }
        }

        return kernelArgs;
    }

    private static object? UnwrapResult(FunctionResult result)
    {
        if (result is null)
        {
            return null;
        }

        try
        {
            var value = result.GetValue<object>();
            return value;
        }
        catch
        {
            // If GetValue<object> fails, return the raw value
            return result;
        }
    }

    private static string ToJavaScriptSafeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        // Convert CamelCase to snake_case
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
