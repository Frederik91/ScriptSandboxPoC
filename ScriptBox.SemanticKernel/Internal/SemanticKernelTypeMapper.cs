using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ScriptBox.SemanticKernel.Internal;

internal static class SemanticKernelTypeMapper
{
    public static string ToTypeScriptType(Type type)
    {
        if (type is null)
        {
            return "unknown";
        }

        if (type == typeof(string) || type == typeof(char))
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "boolean";
        }

        if (IsNumber(type))
        {
            return "number";
        }

        if (type == typeof(object))
        {
            return "unknown";
        }

        if (type == typeof(Guid))
        {
            return "string";
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan))
        {
            return "string";
        }

        if (typeof(JsonElement).IsAssignableFrom(type))
        {
            return "any";
        }

        if (type.IsEnum)
        {
            return "string";
        }

        if (type.IsArray)
        {
            var elementType = ToTypeScriptType(type.GetElementType()!);
            return $"{elementType}[]";
        }

        if (TryMapDictionary(type, out var dictionaryType))
        {
            return dictionaryType;
        }

        if (TryMapEnumerable(type, out var enumerableType))
        {
            return enumerableType;
        }

        return "any";
    }

    private static bool IsNumber(Type type)
    {
        return type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);
    }

    private static bool TryMapDictionary(Type type, out string dictionaryType)
    {
        dictionaryType = "Record<string, any>";
        if (!type.IsGenericType)
        {
            return false;
        }

        var openType = type.GetGenericTypeDefinition();
        if (openType == typeof(Dictionary<,>) ||
            openType == typeof(IDictionary<,>) ||
            openType == typeof(IReadOnlyDictionary<,>))
        {
            var genericArgs = type.GetGenericArguments();
            if (genericArgs[0] == typeof(string))
            {
                var valueType = ToTypeScriptType(genericArgs[1]);
                dictionaryType = $"Record<string, {valueType}>";
                return true;
            }
        }

        return false;
    }

    private static bool TryMapEnumerable(Type type, out string enumerableType)
    {
        enumerableType = "any[]";
        if (type == typeof(string))
        {
            return false;
        }

        if (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
        {
            var genericArg = type.GetGenericArguments().First();
            var elementType = ToTypeScriptType(genericArg);
            enumerableType = $"{elementType}[]";
            return true;
        }

        return false;
    }
}
