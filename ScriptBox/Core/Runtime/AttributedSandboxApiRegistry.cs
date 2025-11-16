using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ScriptBox.Core.Runtime;

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
        HostApiBuilder builder,
        Func<Type, object?>? resolveInstance)
    {
        foreach (var api in apis)
        {
            object? instance = null;
            if (api.RequiresInstance)
            {
                var resolver = resolveInstance ?? DefaultInstanceFactory;
                instance = resolver(api.ApiType);
                if (instance is null)
                {
                    throw new InvalidOperationException($"API factory returned null for type {api.ApiType.FullName}.");
                }
            }

            foreach (var method in api.Methods)
            {
                builder.RegisterJsonHandler(method.HostMethodName, CreateHandler(method, instance));
            }
        }
    }

    private static object? DefaultInstanceFactory(Type type)
    {
        var instance = Activator.CreateInstance(type);
        if (instance is null)
        {
            throw new InvalidOperationException($"Failed to create instance of {type.FullName}.");
        }

        return instance;
    }

    private static Func<HostCallContext, Task<object?>> CreateHandler(
        SandboxMethodDescriptor descriptor,
        object? target)
    {
        return async ctx =>
        {
            var arguments = BindArguments(descriptor, ctx);
            var result = descriptor.Method.Invoke(target, arguments);
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
                var fullName = type.FullName;
                if (!string.IsNullOrEmpty(fullName) && fullName.StartsWith("System.Threading.Tasks.ValueTask`1", StringComparison.Ordinal))
                {
                    var asTaskMethod = type.GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance);
                    if (asTaskMethod != null)
                    {
                        if (asTaskMethod.Invoke(result, null) is Task task)
                        {
                            await task.ConfigureAwait(false);
                            return GetTaskResult(task);
                        }
                    }
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
                if (TryCreateDescriptor(type, out var descriptor))
                {
                    descriptors.Add(descriptor);
                }
            }
        }

        return descriptors;
    }

    public static bool TryCreateDescriptor(Type type, [NotNullWhen(true)] out SandboxApiDescriptor? descriptor)
    {
        descriptor = null;

        var apiAttribute = type.GetCustomAttribute<SandboxApiAttribute>();
        if (apiAttribute is null)
        {
            return false;
        }

        var isStatic = type.IsAbstract && type.IsSealed;

        var methods = new List<SandboxMethodDescriptor>();
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
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

        if (!isStatic)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
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
        }

        if (methods.Count == 0)
        {
            throw new InvalidOperationException(
                $"Sandbox API '{type.FullName}' does not declare any methods marked with [SandboxMethod].");
        }

        descriptor = new SandboxApiDescriptor(type, apiAttribute.Name, methods, RequiresInstance: !isStatic);
        return true;
    }
}

internal sealed record SandboxApiDescriptor(
    Type ApiType,
    string JsNamespace,
    IReadOnlyList<SandboxMethodDescriptor> Methods,
    bool RequiresInstance);

internal sealed record SandboxMethodDescriptor(string JsNamespace, string JsMethodName, MethodInfo Method)
{
    public string HostMethodName => $"{JsNamespace}.{JsMethodName}";
}
