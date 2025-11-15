using System.Reflection;
using System.Text.Json;

namespace ScriptBox.Net.Core.HostApi;

/// <summary>
/// Registry for custom host APIs that can be called from JavaScript.
/// Allows dynamic registration of APIs without modifying core code.
/// </summary>
public class ApiRegistry
{
    private readonly Dictionary<string, RegisteredApi> _apis = new();

    /// <summary>
    /// Registers an API instance with a namespace.
    /// Methods marked with [HostMethod] will be automatically discoverable.
    /// </summary>
    /// <param name="namespace">The JavaScript namespace (e.g., "custom" -> scriptbox.custom.*)</param>
    /// <param name="instance">The API instance containing methods to expose</param>
    public void Register(string @namespace, object instance)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
            throw new ArgumentException("Namespace cannot be null or empty", nameof(@namespace));

        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        var methods = DiscoverMethods(instance);
        _apis[@namespace] = new RegisteredApi
        {
            Instance = instance,
            Methods = methods
        };
    }

    /// <summary>
    /// Attempts to invoke a registered method.
    /// </summary>
    /// <param name="fullMethodName">Full method name in format "namespace.methodName"</param>
    /// <param name="args">JSON element containing arguments</param>
    /// <param name="result">The JSON result if successful</param>
    /// <returns>True if the method was found and invoked, false otherwise</returns>
    public bool TryInvoke(string fullMethodName, JsonElement args, out string result)
    {
        result = string.Empty;

        var parts = fullMethodName.Split('.', 2);
        if (parts.Length != 2)
            return false;

        var @namespace = parts[0];
        var methodName = parts[1];

        if (!_apis.TryGetValue(@namespace, out var api))
            return false;

        if (!api.Methods.TryGetValue(methodName, out var methodInfo))
            return false;

        try
        {
            var parameters = methodInfo.GetParameters();
            var arguments = new object?[parameters.Length];

            // Convert JSON args to method parameters
            for (int i = 0; i < parameters.Length && i < args.GetArrayLength(); i++)
            {
                var param = parameters[i];
                var argElement = args[i];

                arguments[i] = ConvertJsonElement(argElement, param.ParameterType);
            }

            // Invoke the method
            var returnValue = methodInfo.Invoke(api.Instance, arguments);

            // Serialize result
            if (returnValue == null)
            {
                result = "{\"result\":null}";
            }
            else if (returnValue is string strResult)
            {
                result = JsonSerializer.Serialize(new { result = strResult });
            }
            else if (IsSimpleType(returnValue.GetType()))
            {
                result = $"{{\"result\":{JsonSerializer.Serialize(returnValue)}}}";
            }
            else
            {
                result = $"{{\"result\":{JsonSerializer.Serialize(returnValue)}}}";
            }

            return true;
        }
        catch (Exception ex)
        {
            result = JsonSerializer.Serialize(new { error = ex.InnerException?.Message ?? ex.Message });
            return true; // We handled it, just with an error
        }
    }

    /// <summary>
    /// Gets all registered namespaces and their method names for documentation/introspection.
    /// </summary>
    public Dictionary<string, List<string>> GetRegisteredApis()
    {
        return _apis.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Methods.Keys.ToList()
        );
    }

    /// <summary>
    /// Generates JavaScript code to create the API stubs for all registered APIs.
    /// </summary>
    public string GenerateJavaScriptStubs()
    {
        if (_apis.Count == 0)
            return string.Empty;

        var code = new System.Text.StringBuilder();

        foreach (var (ns, api) in _apis)
        {
            // Create namespace object
            code.AppendLine($"scriptbox.{ns} = {{}};");

            foreach (var methodName in api.Methods.Keys)
            {
                // Create method function - use proper JavaScript escaping
                code.Append($"scriptbox.{ns}.{methodName} = function() {{");
                code.Append("var args = Array.prototype.slice.call(arguments);");
                code.Append($"var response = __host.bridge(JSON.stringify({{method:'{ns}.{methodName}',args:args}}));");
                code.Append("var parsed = JSON.parse(response);");
                code.Append("if(parsed.error)throw new Error(parsed.error);");
                code.Append("return parsed.result;");
                code.AppendLine("};");
            }
        }

        return code.ToString();
    }

    /// <summary>
    /// Generates TypeScript definitions for all registered APIs.
    /// </summary>
    public string GenerateTypeScriptDefinitions()
    {
        var code = new System.Text.StringBuilder();

        foreach (var (ns, api) in _apis)
        {
            code.AppendLine($"    {ns}: {{");

            foreach (var (methodName, methodInfo) in api.Methods)
            {
                var parameters = methodInfo.GetParameters();
                var paramList = string.Join(", ", parameters.Select(p =>
                    $"{p.Name}: {GetTypeScriptType(p.ParameterType)}"));

                var returnType = GetTypeScriptType(methodInfo.ReturnType);

                code.AppendLine($"        {methodName}({paramList}): {returnType};");
            }

            code.AppendLine("    };");
        }

        return code.ToString();
    }

    private Dictionary<string, MethodInfo> DiscoverMethods(object instance)
    {
        var methods = new Dictionary<string, MethodInfo>();
        var type = instance.GetType();

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = method.GetCustomAttribute<HostMethodAttribute>();
            if (attr == null)
                continue;

            var methodName = attr.MethodName ?? method.Name;
            methods[methodName] = method;
        }

        return methods;
    }

    private object? ConvertJsonElement(JsonElement element, Type targetType)
    {
        if (targetType == typeof(string))
            return element.GetString();
        if (targetType == typeof(int))
            return element.GetInt32();
        if (targetType == typeof(long))
            return element.GetInt64();
        if (targetType == typeof(double))
            return element.GetDouble();
        if (targetType == typeof(bool))
            return element.GetBoolean();
        if (targetType == typeof(object))
            return element.GetRawText();

        // For complex types, deserialize from JSON
        return JsonSerializer.Deserialize(element.GetRawText(), targetType);
    }

    private bool IsSimpleType(Type type)
    {
        return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
    }

    private string GetTypeScriptType(Type type)
    {
        if (type == typeof(void))
            return "void";
        if (type == typeof(string))
            return "string";
        if (type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(decimal))
            return "number";
        if (type == typeof(bool))
            return "boolean";
        if (type.IsArray)
            return GetTypeScriptType(type.GetElementType()!) + "[]";

        return "any";
    }

    private class RegisteredApi
    {
        public object Instance { get; set; } = null!;
        public Dictionary<string, MethodInfo> Methods { get; set; } = new();
    }
}
