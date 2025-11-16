using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SemanticKernel;
using ScriptBox;

namespace ScriptBox.SemanticKernel;

/// <summary>
/// Extension methods for wiring ScriptBox into a Semantic Kernel builder.
/// </summary>
public static class KernelBuilderScriptBoxExtensions
{
    /// <summary>
    /// Adds a configured ScriptBox instance plus the <see cref="ScriptBoxPlugin"/> to the kernel builder.
    /// </summary>
    /// <param name="builder">The kernel builder to enrich.</param>
    /// <param name="configure">Optional callback for configuring the underlying <see cref="ScriptBoxBuilder"/>.</param>
    /// <param name="pluginName">The plugin name exposed to Semantic Kernel (defaults to "scriptbox").</param>
    /// <returns>The provided builder to enable chaining.</returns>
    public static IKernelBuilder AddScriptBox(
        this IKernelBuilder builder,
        Action<ScriptBoxBuilder>? configure = null,
        string pluginName = "scriptbox")
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (string.IsNullOrWhiteSpace(pluginName))
        {
            throw new ArgumentException("Plugin name must be provided.", nameof(pluginName));
        }

        builder.Services.TryAddSingleton<IScriptBox>(services =>
        {
            var scriptBoxBuilder = ScriptBoxBuilder
                .Create()
                .UseApiFactory(type => ActivatorUtilities.GetServiceOrCreateInstance(services, type));

            configure?.Invoke(scriptBoxBuilder);

            return scriptBoxBuilder.Build();
        });

        builder.Plugins.AddFromType<ScriptBoxPlugin>(pluginName);

        return builder;
    }
}
