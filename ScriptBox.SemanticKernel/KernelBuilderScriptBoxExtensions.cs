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
    /// Also registers <see cref="SemanticKernelToolProvider"/> so tools can be discovered and exposed to scripts.
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

        // Register a tool provider factory that will be resolved after the Kernel exists
        // The trick: ScriptBoxPlugin receives the Kernel via the service provider when plugins are instantiated
        builder.Services.TryAddSingleton<Func<Kernel, IScriptBoxToolProvider>>(sp =>
        {
            return kernel => new SemanticKernelToolProvider(kernel);
        });

        // Register a lazy wrapper that defers Kernel access until ScriptBoxPlugin needs it
        builder.Services.TryAddSingleton<IScriptBoxToolProvider>(sp =>
        {
            // Create a temporary tool provider that will fetch the Kernel from service provider on first access
            return new LazySemanticKernelToolProvider(() => sp.GetRequiredService<Kernel>());
        });

        builder.Plugins.AddFromType<ScriptBoxPlugin>(pluginName);

        return builder;
    }

    /// <summary>
    /// Lazy wrapper that defers Kernel lookup until tools are actually requested.
    /// Avoids circular dependency issues during plugin registration.
    /// </summary>
    private sealed class LazySemanticKernelToolProvider : IScriptBoxToolProvider
    {
        private readonly Func<Kernel> _kernelFactory;
        private IScriptBoxToolProvider? _innerProvider;

        public LazySemanticKernelToolProvider(Func<Kernel> kernelFactory)
        {
            _kernelFactory = kernelFactory ?? throw new ArgumentNullException(nameof(kernelFactory));
        }

        public IReadOnlyList<ScriptBoxToolDescriptor> GetTools()
        {
            _innerProvider ??= new SemanticKernelToolProvider(_kernelFactory());
            return _innerProvider.GetTools();
        }

        public Task<object?> InvokeToolAsync(string toolId, object? args, CancellationToken cancellationToken = default)
        {
            _innerProvider ??= new SemanticKernelToolProvider(_kernelFactory());
            return _innerProvider.InvokeToolAsync(toolId, args, cancellationToken);
        }
    }
}
