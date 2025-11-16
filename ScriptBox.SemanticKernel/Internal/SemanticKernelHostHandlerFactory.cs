using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ScriptBox.Core.Runtime;

namespace ScriptBox.SemanticKernel.Internal;

internal static class SemanticKernelHostHandlerFactory
{
    public static Func<HostCallContext, Task<object?>> CreateHandler(
        MethodInfo method,
        object? target)
    {
        return async ctx =>
        {
            var arguments = BindArguments(method, ctx);
            var result = method.Invoke(target, arguments);
            return await UnwrapResultAsync(result).ConfigureAwait(false);
        };
    }

    private static object?[] BindArguments(MethodInfo method, HostCallContext ctx)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return Array.Empty<object?>();
        }

        var values = new object?[parameters.Length];
        var argIndex = 0;
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter.ParameterType == typeof(HostCallContext))
            {
                values[i] = ctx;
                continue;
            }

            if (parameter.ParameterType == typeof(CancellationToken))
            {
                values[i] = ctx.CancellationToken;
                continue;
            }

            if (argIndex >= ctx.Arguments.Count)
            {
                throw new InvalidOperationException(
                    $"Not enough arguments supplied for method '{method.Name}'. Expected {parameters.Length}.");
            }

            values[i] = ConvertValue(ctx.Arguments[argIndex++], parameter.ParameterType);
        }

        return values;
    }

    private static object? ConvertValue(object? raw, Type targetType)
    {
        if (raw is null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType.IsInstanceOfType(raw))
        {
            return raw;
        }

        if (raw is JsonElement element)
        {
            var json = element.GetRawText();
            return JsonSerializer.Deserialize(json, targetType);
        }

        if (targetType.IsEnum)
        {
            if (raw is string enumName)
            {
                return Enum.Parse(targetType, enumName, ignoreCase: true);
            }

            return Enum.ToObject(targetType, raw);
        }

        if (targetType == typeof(Guid))
        {
            return Guid.Parse(Convert.ToString(raw, CultureInfo.InvariantCulture)!);
        }

        return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
    }

    private static async Task<object?> UnwrapResultAsync(object? result)
    {
        switch (result)
        {
            case null:
                return null;
            case Task task when task.GetType().IsGenericType:
                await task.ConfigureAwait(false);
                return GetTaskResult(task);
            case Task task:
                await task.ConfigureAwait(false);
                return null;
            case ValueTask valueTask:
                await valueTask.ConfigureAwait(false);
                return null;
            default:
                var type = result.GetType();
                if (TryUnwrapGenericValueTask(type, result, out var valueTaskResult))
                {
                    return valueTaskResult;
                }

                return result;
        }
    }

    private static bool TryUnwrapGenericValueTask(Type type, object instance, out object? value)
    {
        value = null;
        if (!type.FullName!.StartsWith("System.Threading.Tasks.ValueTask`1", StringComparison.Ordinal))
        {
            return false;
        }

        var asTaskMethod = type.GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance);
        if (asTaskMethod?.Invoke(instance, null) is Task task)
        {
            task.GetAwaiter().GetResult();
            value = GetTaskResult(task);
            return true;
        }

        return false;
    }

    private static object? GetTaskResult(Task task)
    {
        var taskType = task.GetType();
        var resultProperty = taskType.GetProperty("Result", BindingFlags.Instance | BindingFlags.Public);
        return resultProperty?.GetValue(task);
    }
}
