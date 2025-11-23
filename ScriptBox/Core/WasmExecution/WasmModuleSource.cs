using System;
using System.IO;
using Wasmtime;

namespace ScriptBox.Core.WasmExecution;

/// <summary>
/// Describes how to load the QuickJS WASM module. Either from disk or from
/// an in-memory byte array supplied by the builder.
/// </summary>
internal sealed class WasmModuleSource
{
    private readonly string? _path;
    private readonly byte[]? _moduleBytes;
    private readonly string _description;

    private WasmModuleSource(string path)
    {
        _path = Path.GetFullPath(path);
        _description = $"file://{_path}";
    }

    private WasmModuleSource(byte[] moduleBytes)
    {
        _moduleBytes = moduleBytes ?? throw new ArgumentNullException(nameof(moduleBytes));
        _description = "in-memory module";
    }

    public static WasmModuleSource FromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("WASM path cannot be null or empty", nameof(path));
        }

        return new WasmModuleSource(path);
    }

    public static WasmModuleSource FromBytes(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            throw new ArgumentException("WASM module bytes cannot be empty", nameof(bytes));
        }

        return new WasmModuleSource(bytes.ToArray());
    }

    public Module CreateModule(Engine engine)
    {
        if (engine == null)
        {
            throw new ArgumentNullException(nameof(engine));
        }

        if (_path is not null)
        {
            if (!File.Exists(_path))
            {
                throw new FileNotFoundException($"WASM module not found at {_path}");
            }

            return Module.FromFile(engine, _path);
        }

        if (_moduleBytes is null)
        {
            throw new InvalidOperationException("Module source not initialized.");
        }

        return Module.FromBytes(engine, "scriptbox", _moduleBytes);
    }

    public override string ToString() => _description;
}
