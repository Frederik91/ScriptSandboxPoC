using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ScriptBox.Core.Configuration;
using ScriptBox.Core.Runtime;
using ScriptBox.Core.WasmExecution;

namespace ScriptBox;

/// <summary>
/// Fluent builder for configuring ScriptBox instances.
/// </summary>
public sealed class ScriptBoxBuilder
{
    private readonly HostApiBuilder _hostApiBuilder = new();
    private readonly List<AllowedDirectory> _allowedDirectories = new();
    private readonly Dictionary<string, string?> _envVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Func<CancellationToken, Task<string>>> _bootstrapLoaders = new();
    private readonly List<Type> _registeredApiTypes = new();
    private readonly Dictionary<string, object> _metadata = new();
    private readonly List<ISandboxApiScanner> _apiScanners = new();
    private string? _wasmModulePath;
    private byte[]? _wasmModuleBytes;
    private TimeSpan _defaultTimeout = TimeSpan.FromMilliseconds(WasmConfiguration.DefaultTimeoutMs);
    private int? _memoryLimitMb;
    private SandboxConfiguration? _sandboxConfiguration;
    private Func<Type, object?>? _apiFactory;

    private ScriptBoxBuilder()
    {
        _bootstrapLoaders.Add(_ => Task.FromResult(DefaultRuntimeResources.LoadCoreBootstrap()));
        _apiScanners.Add(new AttributedSandboxApiScanner());
    }

    public static ScriptBoxBuilder Create() => new();

    public ScriptBoxBuilder AddApiScanner(ISandboxApiScanner scanner)
    {
        if (scanner is null)
        {
            throw new ArgumentNullException(nameof(scanner));
        }
        _apiScanners.Add(scanner);
        return this;
    }

    public ScriptBoxBuilder WithWasmModule(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("WASM path cannot be null or empty", nameof(path));
        }

        _wasmModulePath = path;
        _wasmModuleBytes = null;
        return this;
    }

    public ScriptBoxBuilder WithWasmModule(ReadOnlyMemory<byte> moduleBytes)
    {
        if (moduleBytes.IsEmpty)
        {
            throw new ArgumentException("WASM module bytes cannot be empty", nameof(moduleBytes));
        }

        _wasmModuleBytes = moduleBytes.ToArray();
        _wasmModulePath = null;
        return this;
    }

    public ScriptBoxBuilder WithAdditionalBootstrapFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Bootstrap path cannot be null or empty", nameof(path));
        }

        return WithAdditionalBootstrap(_ => Task.FromResult(BootstrapScriptLoader.LoadScriptFile(path)));
    }

    public ScriptBoxBuilder WithAdditionalBootstrap(Func<CancellationToken, Task<string>> loader)
    {
        if (loader is null)
        {
            throw new ArgumentNullException(nameof(loader));
        }

        _bootstrapLoaders.Add(loader);
        return this;
    }

    public ScriptBoxBuilder WithBootstrapFile(string path) => WithAdditionalBootstrapFile(path);

    public ScriptBoxBuilder WithBootstrap(Func<CancellationToken, Task<string>> loader) =>
        WithAdditionalBootstrap(loader);

    public ScriptBoxBuilder ConfigureHostApi(Func<HostApiBuilder, HostApiBuilder> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        configure(_hostApiBuilder);
        return this;
    }

    public ScriptBoxBuilder WithDefaultTimeout(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be non-negative");
        }

        _defaultTimeout = timeout;
        return this;
    }

    public ScriptBoxBuilder WithMemoryLimitMB(int megabytes)
    {
        if (megabytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(megabytes), "Memory limit must be positive");
        }

        _memoryLimitMb = megabytes;
        return this;
    }

    public ScriptBoxBuilder AllowDirectory(string hostPath, string mountPath, SandboxAccess access)
    {
        if (string.IsNullOrWhiteSpace(hostPath))
        {
            throw new ArgumentException("Host path cannot be null or empty", nameof(hostPath));
        }

        if (string.IsNullOrWhiteSpace(mountPath))
        {
            throw new ArgumentException("Mount path cannot be null or empty", nameof(mountPath));
        }

        _allowedDirectories.Add(new AllowedDirectory(hostPath, mountPath, access));
        return this;
    }

    public ScriptBoxBuilder AllowEnvVariable(string name, string? value = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Variable name cannot be null or empty", nameof(name));
        }

        _envVariables[name] = value;
        return this;
    }

    public ScriptBoxBuilder WithMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        }

        _metadata[key] = value;
        return this;
    }

    public T? GetMetadata<T>(string key)
    {
        if (_metadata.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    public IScriptBox Build()
    {
        ProcessAttributedApis();

        var moduleSource = ResolveModuleSource();
        var configBootstrapScripts = _sandboxConfiguration?.BootstrapScripts?.ToList();
        var bootstrapCode = LoadBootstrapCode(configBootstrapScripts);
        var hostHandlers = _hostApiBuilder.Build();

        var config = _sandboxConfiguration ?? SandboxConfiguration.CreateDefault();
        config.BootstrapScripts = new List<string>(); // Builder injects scripts directly

        var executor = new WasmScriptExecutor(
            hostApi: null,
            config: config,
            jsonHandlers: hostHandlers,
            moduleSource: moduleSource);

        GC.KeepAlive(_allowedDirectories);
        GC.KeepAlive(_envVariables);
        GC.KeepAlive(_memoryLimitMb);

        return new ScriptBox(executor, bootstrapCode, _defaultTimeout, _metadata);
    }

    private WasmModuleSource ResolveModuleSource()
    {
        if (_wasmModuleBytes is not null)
        {
            return WasmModuleSource.FromBytes(_wasmModuleBytes);
        }

        if (!string.IsNullOrWhiteSpace(_wasmModulePath))
        {
            return WasmModuleSource.FromPath(_wasmModulePath!);
        }

        var embedded = DefaultRuntimeResources.LoadEmbeddedWasm();
        return WasmModuleSource.FromBytes(embedded);
    }

    private string LoadBootstrapCode(IEnumerable<string>? configScripts)
    {
        var builder = new StringBuilder();

        foreach (var loader in _bootstrapLoaders)
        {
            var code = loader(CancellationToken.None).GetAwaiter().GetResult();
            AppendBootstrap(builder, code);
        }

        if (configScripts != null)
        {
            foreach (var scriptPath in configScripts)
            {
                var code = BootstrapScriptLoader.LoadScriptFile(scriptPath);
                AppendBootstrap(builder, code);
            }
        }

        return builder.ToString();
    }

    private static void AppendBootstrap(StringBuilder builder, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        builder.AppendLine(code);
        builder.AppendLine();
    }

    public ScriptBoxBuilder ExposeHostApi(string bootstrapCode, Func<HostApiBuilder, HostApiBuilder>? configure)
    {
        if (!string.IsNullOrWhiteSpace(bootstrapCode))
        {
            WithAdditionalBootstrap(_ => Task.FromResult(bootstrapCode));
        }

        if (configure is not null)
        {
            configure(_hostApiBuilder);
        }

        return this;
    }

    public ScriptBoxBuilder RegisterApisFrom<T>()
    {
        return RegisterApisFrom(typeof(T));
    }

    public ScriptBoxBuilder RegisterApisFrom(Type type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        _registeredApiTypes.Add(type);
        return this;
    }

    public ScriptBoxBuilder UseApiFactory(Func<Type, object?> apiFactory)
    {
        _apiFactory = apiFactory ?? throw new ArgumentNullException(nameof(apiFactory));
        return this;
    }

    private void ProcessAttributedApis()
    {
        if (_registeredApiTypes.Count == 0)
        {
            return;
        }

        var descriptors = new List<SandboxApiDescriptor>();
        foreach (var type in _registeredApiTypes)
        {
            SandboxApiDescriptor? descriptor = null;
            foreach (var scanner in _apiScanners)
            {
                if (scanner.TryCreateDescriptor(type, out descriptor))
                {
                    break;
                }
            }

            if (descriptor is null)
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' could not be processed by any registered API scanner. Ensure it has the correct attributes (e.g. [SandboxApi]).");
            }

            descriptors.Add(descriptor);
        }

        if (descriptors.Count == 0)
        {
            return;
        }

        var bootstrap = AttributedSandboxApiRegistry.BuildBootstrap(descriptors);
        if (!string.IsNullOrWhiteSpace(bootstrap))
        {
            _bootstrapLoaders.Add(_ => Task.FromResult(bootstrap));
        }

        AttributedSandboxApiRegistry.RegisterHandlers(
            descriptors,
            _hostApiBuilder,
            ResolveApiInstance);
    }

    private object ResolveApiInstance(Type apiType)
    {
        if (apiType is null)
        {
            throw new ArgumentNullException(nameof(apiType));
        }

        object? instance = null;
        if (_apiFactory is not null)
        {
            instance = _apiFactory(apiType);
        }

        if (instance is null)
        {
            instance = Activator.CreateInstance(apiType);
        }

        if (instance is null)
        {
            throw new InvalidOperationException(
                $"Unable to create API instance for type '{apiType.FullName}'. Provide a factory via UseApiFactory.");
        }

        return instance;
    }

    public ScriptBoxBuilder WithSandboxConfiguration(SandboxConfiguration configuration)
    {
        _sandboxConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        return this;
    }

    private sealed record AllowedDirectory(string HostPath, string MountPath, SandboxAccess Access);
}
