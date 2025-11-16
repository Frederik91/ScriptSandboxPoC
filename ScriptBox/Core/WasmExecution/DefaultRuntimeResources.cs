using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace ScriptBox.Core.WasmExecution;

internal static class DefaultRuntimeResources
{
    private const string WasmResourceName = "ScriptBox.Wasm.scriptbox.wasm";
    private const string CoreBootstrapResourceName = "ScriptBox.Js.sandbox-api.js";

    public static ReadOnlyMemory<byte> LoadEmbeddedWasm()
    {
        return ReadAllBytes(WasmResourceName);
    }

    public static string LoadCoreBootstrap()
    {
        return ReadAllText(CoreBootstrapResourceName);
    }

    private static ReadOnlyMemory<byte> ReadAllBytes(string resourceName)
    {
        using var stream = OpenResourceStream(resourceName);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string ReadAllText(string resourceName)
    {
        using var stream = OpenResourceStream(resourceName);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static Stream OpenResourceStream(string resourceName)
    {
        var assembly = typeof(DefaultRuntimeResources).GetTypeInfo().Assembly;
        return assembly.GetManifestResourceStream(resourceName)
               ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
    }
}
