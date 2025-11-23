using System;
using System.Threading;
using System.Threading.Tasks;
using ScriptBox.Core.Configuration;

namespace ScriptBox;

/// <summary>
/// Interface for configuring ScriptBox instances without exposing the Build method.
/// </summary>
public interface IScriptBoxConfigurator
{
    IScriptBoxConfigurator WithWasmModuleFromPath(string path);
    IScriptBoxConfigurator WithWasmModule(ReadOnlyMemory<byte> moduleBytes);
    IScriptBoxConfigurator WithStartupFile(string path);
    IScriptBoxConfigurator WithStartupScript(Func<CancellationToken, Task<string>> loader);
    IScriptBoxConfigurator WithExecutionTimeout(TimeSpan timeout);
    IScriptBoxConfigurator RegisterApisFrom<T>(string? name = null);
    IScriptBoxConfigurator RegisterApisFrom(Type type, string? name = null);
    IScriptBoxConfigurator AddFromType<T>(string? name = null);
    IScriptBoxConfigurator AddFromObject(object instance, string? name = null);
    IScriptBoxConfigurator WithApiFactory(Func<Type, object?> apiFactory);
    IScriptBoxConfigurator WithMetadata(string key, object value);
    IScriptBoxConfigurator WithSandboxConfiguration(SandboxConfiguration configuration);
    IScriptBoxConfigurator ConfigureFileSystem(Action<ScriptBoxBuilder.FileSystemConfigurationBuilder> configure);
    IScriptBoxConfigurator ConfigureNetwork(Action<ScriptBoxBuilder.NetworkConfigurationBuilder> configure);
}
