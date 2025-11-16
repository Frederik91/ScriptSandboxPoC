using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ScriptBox.Net.Core.Runtime;

internal static class AttributedSandboxApiRegistry
{
    public static string BuildBootstrap(IEnumerable<SandboxApiDescriptor> apis)
    {
        var sb = new StringBuilder();
        foreach (var api in apis)
        {
            if (api.Methods.Count == 0)
            {
                continue;
            }

            sb.AppendLine("(function(root){");
            sb.AppendLine("  if (typeof __scriptbox === 'undefined') {");
            sb.AppendLine("    throw new Error('Missing __scriptbox helper.');");
            sb.AppendLine("  }");
            sb.AppendLine("  var api = {};");
            foreach (var method in api.Methods)
            {
                sb.AppendLine($"  api.{method.JsMethodName} = __scriptbox.createMethod('{method.HostMethodName}');");
            }
            sb.AppendLine($"  root.{api.JsNamespace} = api;");
            sb.Append("})(");
            sb.Append("typeof globalThis !== 'undefined' ? globalThis : ");
            sb.Append("typeof global !== 'undefined' ? global : ");
            sb.Append("typeof self !== 'undefined' ? self : this");
            sb.AppendLine(");");
        }

        return sb.ToString();
    }

    public static void RegisterHandlers(
        IEnumerable<SandboxApiDescriptor> apis,
        HostApiBuilder builder)
    {
        foreach (var api in apis)
        {
            foreach (var method in api.Methods)
            {
                builder.RegisterJsonHandler(method.HostMethodName, CreateHandler(method));
            }
        }
    }

    private static Func<HostCallContext, Task<object?>> CreateHandler(SandboxMethodDescriptor descriptor)
    {
        return async ctx =>
        {
            var arguments = BindArguments(descriptor, ctx);
            var result = descriptor.Method.Invoke(null, arguments);
            return await UnwrapResultAsync(result);
        };
    }

    private static object?[] BindArguments(SandboxMethodDescriptor descriptor, HostCallContext ctx)
    {
        var parameters = descriptor.Method.GetParameters();
        var values = new object?[parameters.Length];
        var argIndex = 0;

        for (int i = 0; i < parameters.Length; i++)
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
                    $"Not enough arguments supplied for method '{descriptor.HostMethodName}'. Expected {parameters.Length}");
            }

            var raw = ctx.Arguments[argIndex++];
            values[i] = ConvertValue(raw, parameter.ParameterType);
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
                if (type.FullName is { } fullName && fullName.StartsWith("System.Threading.Tasks.ValueTask`1"))
                {
                    dynamic dynamicTask = result!;
                    return await dynamicTask.ConfigureAwait(false);
                }

                return result;
        }
    }

    private static object? GetTaskResult(Task task)
    {
        var taskType = task.GetType();
        var resultProperty = taskType.GetProperty("Result", BindingFlags.Instance | BindingFlags.Public);
        return resultProperty?.GetValue(task);
    }

    public static IReadOnlyList<SandboxApiDescriptor> DiscoverApis(IEnumerable<Assembly> assemblies)
    {
        var descriptors = new List<SandboxApiDescriptor>();
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                var apiAttribute = type.GetCustomAttribute<SandboxApiAttribute>();
                if (apiAttribute is null)
                {
                    continue;
                }

                if (!type.IsAbstract || !type.IsSealed)
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' must be a static class to be used as a Sandbox API.");
                }

                var methods = new List<SandboxMethodDescriptor>();
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var methodAttribute = method.GetCustomAttribute<SandboxMethodAttribute>();
                    if (methodAttribute is null)
                    {
                        continue;
                    }

                    methods.Add(new SandboxMethodDescriptor(
                        apiAttribute.Name,
                        methodAttribute.Name,
                        method));
                }

                if (methods.Count > 0)
                {
                    descriptors.Add(new SandboxApiDescriptor(apiAttribute.Name, methods));
                }
            }
        }

        return descriptors;
    }
}

internal sealed record SandboxApiDescriptor(string JsNamespace, IReadOnlyList<SandboxMethodDescriptor> Methods);

internal sealed record SandboxMethodDescriptor(string JsNamespace, string JsMethodName, MethodInfo Method)
{
    public string HostMethodName => $"{JsNamespace}.{JsMethodName}";
}
