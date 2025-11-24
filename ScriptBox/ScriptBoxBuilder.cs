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
public sealed class ScriptBoxBuilder : IScriptBoxConfigurator
{
    private static readonly List<Func<ISandboxApiScanner>> _defaultScannerFactories = new();
    private static readonly object _scannerLock = new();

    private readonly HostApiBuilder _hostApiBuilder = new();
    private readonly List<Func<CancellationToken, Task<string>>> _startupScriptLoaders = new();
    private readonly List<(Type Type, string? Namespace)> _registeredApiTypes = new();
    private readonly List<(object Instance, string? Namespace)> _registeredApiInstances = new();
    private readonly Dictionary<string, object> _metadata = new();
    private readonly List<ISandboxApiScanner> _apiScanners = new();
    private string? _wasmModulePath;
    private byte[]? _wasmModuleBytes;
    private TimeSpan _executionTimeout = TimeSpan.FromMilliseconds(WasmConfiguration.DefaultTimeoutMs);
    private SandboxConfiguration? _sandboxConfiguration;
    private Func<Type, object?>? _apiFactory;

    private ScriptBoxBuilder()
    {
        _startupScriptLoaders.Add(_ => Task.FromResult(DefaultRuntimeResources.LoadCoreBootstrap()));
        _apiScanners.Add(new AttributedSandboxApiScanner());

        // Add all registered default scanners
        lock (_scannerLock)
        {
            foreach (var factory in _defaultScannerFactories)
            {
                _apiScanners.Add(factory());
            }
        }
    }

    public static ScriptBoxBuilder Create() => new();

    /// <summary>
    /// Registers a default scanner factory that will be automatically added to all new ScriptBoxBuilder instances.
    /// This is typically called by package module initializers (e.g., ScriptBox.SemanticKernel).
    /// </summary>
    /// <param name="scannerFactory">Factory function that creates a scanner instance.</param>
    public static void RegisterDefaultScanner(Func<ISandboxApiScanner> scannerFactory)
    {
        if (scannerFactory is null)
        {
            throw new ArgumentNullException(nameof(scannerFactory));
        }

        lock (_scannerLock)
        {
            _defaultScannerFactories.Add(scannerFactory);
        }
    }

    internal ScriptBoxBuilder WithApiScanner(ISandboxApiScanner scanner)
    {
        if (scanner is null)
        {
            throw new ArgumentNullException(nameof(scanner));
        }
        _apiScanners.Add(scanner);
        return this;
    }

    public ScriptBoxBuilder WithWasmModuleFromPath(string path)
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

    public ScriptBoxBuilder WithStartupFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Startup script path cannot be null or empty", nameof(path));
        }

        return WithStartupScript(_ => Task.FromResult(BootstrapScriptLoader.LoadScriptFile(path)));
    }

    public ScriptBoxBuilder WithStartupScript(Func<CancellationToken, Task<string>> loader)
    {
        if (loader is null)
        {
            throw new ArgumentNullException(nameof(loader));
        }

        _startupScriptLoaders.Add(loader);
        return this;
    }

    public ScriptBoxBuilder WithExecutionTimeout(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be non-negative");
        }

        _executionTimeout = timeout;
        return this;
    }

    public ScriptBoxBuilder RegisterApisFrom<T>(string? name = null)
    {
        return RegisterApisFrom(typeof(T), name);
    }

    public ScriptBoxBuilder RegisterApisFrom(Type type, string? name = null)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        _registeredApiTypes.Add((type, name));
        return this;
    }

    public ScriptBoxBuilder AddFromType<T>(string? name = null)
    {
        return RegisterApisFrom(typeof(T), name);
    }

    public ScriptBoxBuilder AddFromObject(object instance, string? name = null)
    {
        if (instance is null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        _registeredApiInstances.Add((instance, name));
        return this;
    }

    public ScriptBoxBuilder WithApiFactory(Func<Type, object?> apiFactory)
    {
        _apiFactory = apiFactory ?? throw new ArgumentNullException(nameof(apiFactory));
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
        var configStartupScripts = _sandboxConfiguration?.StartupScripts?.ToList();
        var startupCode = LoadStartupCode(configStartupScripts);
        var hostHandlers = _hostApiBuilder.Build();

        var config = _sandboxConfiguration ?? SandboxConfiguration.CreateDefault();
        config.StartupScripts = new List<string>(); // Builder injects scripts directly

        var executor = new WasmScriptExecutor(
            hostApi: null,
            config: config,
            jsonHandlers: hostHandlers,
            moduleSource: moduleSource);

        return new ScriptBox(executor, startupCode, _executionTimeout, _metadata);
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

    private string LoadStartupCode(IEnumerable<string>? configScripts)
    {
        var builder = new StringBuilder();

        foreach (var loader in _startupScriptLoaders)
        {
            var code = loader(CancellationToken.None).GetAwaiter().GetResult();
            AppendStartupScript(builder, code);
        }

        if (configScripts != null)
        {
            foreach (var scriptPath in configScripts)
            {
                var code = BootstrapScriptLoader.LoadScriptFile(scriptPath);
                AppendStartupScript(builder, code);
            }
        }

        return builder.ToString();
    }

    private static void AppendStartupScript(StringBuilder builder, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        builder.AppendLine(code);
        builder.AppendLine();
    }

    internal ScriptBoxBuilder ConfigureHostApi(Func<HostApiBuilder, HostApiBuilder> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        configure(_hostApiBuilder);
        return this;
    }

    private void ProcessAttributedApis()
    {
        if (_registeredApiTypes.Count == 0 && _registeredApiInstances.Count == 0)
        {
            return;
        }

        var descriptorsAndInstances = new List<(SandboxApiDescriptor Descriptor, object? Instance)>();

        foreach (var (type, ns) in _registeredApiTypes)
        {
            SandboxApiDescriptor? descriptor = null;
            foreach (var scanner in _apiScanners)
            {
                if (scanner.TryCreateDescriptor(type, ns, out descriptor))
                {
                    break;
                }
            }

            if (descriptor is null)
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' could not be processed by any registered API scanner. Ensure it has the correct attributes (e.g. [SandboxApi]).");
            }

            descriptorsAndInstances.Add((descriptor, null));
        }

        foreach (var (instance, ns) in _registeredApiInstances)
        {
            var type = instance.GetType();
            SandboxApiDescriptor? descriptor = null;
            foreach (var scanner in _apiScanners)
            {
                if (scanner.TryCreateDescriptor(type, ns, out descriptor))
                {
                    break;
                }
            }

            if (descriptor is null)
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' (from instance) could not be processed by any registered API scanner. Ensure it has the correct attributes (e.g. [SandboxApi]).");
            }

            descriptorsAndInstances.Add((descriptor, instance));
        }

        if (descriptorsAndInstances.Count == 0)
        {
            return;
        }

        var bootstrap = AttributedSandboxApiRegistry.BuildBootstrap(descriptorsAndInstances.Select(x => x.Descriptor));
        if (!string.IsNullOrWhiteSpace(bootstrap))
        {
            _startupScriptLoaders.Add(_ => Task.FromResult(bootstrap));
        }

        AttributedSandboxApiRegistry.RegisterHandlers(
            descriptorsAndInstances,
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
                $"Unable to create API instance for type '{apiType.FullName}'. Provide a factory via WithApiFactory.");
        }

        return instance;
    }

    public ScriptBoxBuilder WithSandboxConfiguration(SandboxConfiguration configuration)
    {
        _sandboxConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        return this;
    }

    public ScriptBoxBuilder ConfigureFileSystem(Action<FileSystemConfigurationBuilder> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var config = _sandboxConfiguration ??= SandboxConfiguration.CreateDefault();
        var builder = new FileSystemConfigurationBuilder(config);
        configure(builder);
        return this;
    }

    public ScriptBoxBuilder ConfigureNetwork(Action<NetworkConfigurationBuilder> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var config = _sandboxConfiguration ??= SandboxConfiguration.CreateDefault();
        var builder = new NetworkConfigurationBuilder(config);
        configure(builder);
        return this;
    }

    #region IScriptBoxConfigurator Explicit Implementation

    IScriptBoxConfigurator IScriptBoxConfigurator.WithWasmModuleFromPath(string path) => WithWasmModuleFromPath(path);
    IScriptBoxConfigurator IScriptBoxConfigurator.WithWasmModule(ReadOnlyMemory<byte> moduleBytes) => WithWasmModule(moduleBytes);
    IScriptBoxConfigurator IScriptBoxConfigurator.WithStartupFile(string path) => WithStartupFile(path);
    IScriptBoxConfigurator IScriptBoxConfigurator.WithStartupScript(Func<CancellationToken, Task<string>> loader) => WithStartupScript(loader);
    IScriptBoxConfigurator IScriptBoxConfigurator.WithExecutionTimeout(TimeSpan timeout) => WithExecutionTimeout(timeout);
    IScriptBoxConfigurator IScriptBoxConfigurator.RegisterApisFrom<T>(string? name) => RegisterApisFrom<T>(name);
    IScriptBoxConfigurator IScriptBoxConfigurator.RegisterApisFrom(Type type, string? name) => RegisterApisFrom(type, name);
    IScriptBoxConfigurator IScriptBoxConfigurator.AddFromType<T>(string? name) => AddFromType<T>(name);
    IScriptBoxConfigurator IScriptBoxConfigurator.AddFromObject(object instance, string? name) => AddFromObject(instance, name);
    IScriptBoxConfigurator IScriptBoxConfigurator.WithApiFactory(Func<Type, object?> apiFactory) => WithApiFactory(apiFactory);
    IScriptBoxConfigurator IScriptBoxConfigurator.WithMetadata(string key, object value) => WithMetadata(key, value);
    IScriptBoxConfigurator IScriptBoxConfigurator.WithSandboxConfiguration(SandboxConfiguration configuration) => WithSandboxConfiguration(configuration);
    IScriptBoxConfigurator IScriptBoxConfigurator.ConfigureFileSystem(Action<FileSystemConfigurationBuilder> configure) => ConfigureFileSystem(configure);
    IScriptBoxConfigurator IScriptBoxConfigurator.ConfigureNetwork(Action<NetworkConfigurationBuilder> configure) => ConfigureNetwork(configure);

    #endregion

    public sealed class FileSystemConfigurationBuilder
    {
        private readonly SandboxConfiguration _config;

        internal FileSystemConfigurationBuilder(SandboxConfiguration config)
        {
            _config = config;
        }

        public FileSystemConfigurationBuilder WithRootDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Sandbox directory cannot be null or empty", nameof(path));
            }
            _config.SandboxDirectory = path;
            return this;
        }

        public FileSystemConfigurationBuilder WithConsentHook(Func<FileSystemConsentContext, bool> hook)
        {
            _config.FileSystemConsentHook = hook;
            return this;
        }
    }

    public sealed class NetworkConfigurationBuilder
    {
        private readonly SandboxConfiguration _config;

        internal NetworkConfigurationBuilder(SandboxConfiguration config)
        {
            _config = config;
        }

        public NetworkConfigurationBuilder WithAllowedDomains(params string[] domains)
        {
            _config.AllowedHttpDomains ??= new List<string>();
            if (domains != null)
            {
                _config.AllowedHttpDomains.AddRange(domains);
            }
            return this;
        }

        public NetworkConfigurationBuilder ConfigureHttpClient(Action<System.Net.Http.HttpClient> configure)
        {
            _config.HttpClientConfigurator = configure;
            return this;
        }

        public NetworkConfigurationBuilder WithHttpClient(Func<System.Net.Http.HttpClient> factory)
        {
            _config.HttpClientFactory = factory;
            return this;
        }

        public NetworkConfigurationBuilder WithConsentHook(Func<NetworkConsentContext, bool> hook)
        {
            _config.NetworkConsentHook = hook;
            return this;
        }

        public NetworkConfigurationBuilder WithRequestTimeout(TimeSpan timeout)
        {
            if (timeout.TotalMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive");
            }
            _config.HttpTimeoutMs = (int)timeout.TotalMilliseconds;
            return this;
        }

        public NetworkConfigurationBuilder WithMaxResponseSize(int maxBytes)
        {
            if (maxBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes), "Size must be positive");
            }
            _config.MaxHttpResponseSize = maxBytes;
            return this;
        }
    }
}
