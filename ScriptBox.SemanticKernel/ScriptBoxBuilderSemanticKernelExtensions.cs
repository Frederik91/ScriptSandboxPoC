using ScriptBox;
using ScriptBox.SemanticKernel.Internal;

namespace ScriptBox.SemanticKernel;

/// <summary>
/// Extension methods that add Semantic Kernel annotated plugins to the ScriptBox host surface.
/// </summary>
public static class ScriptBoxBuilderSemanticKernelExtensions
{
    /// <summary>
    /// Registers a Semantic Kernel plugin type as a ScriptBox namespace.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin type containing [KernelFunction] methods.</typeparam>
    /// <param name="builder">The ScriptBox builder instance.</param>
    /// <param name="jsNamespace">The namespace that will be available in the sandbox.</param>
    /// <param name="pluginFactory">Optional factory for creating the plugin instance.</param>
    /// <returns>Metadata describing the registered namespace for TypeScript generation.</returns>
    public static SemanticKernelNamespaceMetadata RegisterSemanticKernelPlugin<TPlugin>(
        this ScriptBoxBuilder builder,
        string jsNamespace,
        Func<TPlugin>? pluginFactory = null)
        where TPlugin : class
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (string.IsNullOrWhiteSpace(jsNamespace))
        {
            throw new ArgumentException("Namespace must be provided.", nameof(jsNamespace));
        }

        var descriptor = SemanticKernelPluginScanner.CreateDescriptor(typeof(TPlugin), jsNamespace);
        var pluginInstance = descriptor.RequiresInstance ? CreateInstance(pluginFactory, descriptor.PluginType) : null;

        builder.ExposeHostApi(descriptor.BootstrapCode, api =>
        {
            foreach (var function in descriptor.Functions)
            {
                var target = function.IsStatic ? null : pluginInstance;
                api.RegisterJsonHandler(function.HostMethodName, SemanticKernelHostHandlerFactory.CreateHandler(function.Method, target));
            }

            return api;
        });

        var list = builder.GetMetadata<List<SemanticKernelNamespaceMetadata>>("SemanticKernelPlugins");
        if (list == null)
        {
            list = new List<SemanticKernelNamespaceMetadata>();
            builder.WithMetadata("SemanticKernelPlugins", list);
        }
        list.Add(descriptor.Metadata);

        return descriptor.Metadata;
    }

    private static object CreateInstance<TPlugin>(Func<TPlugin>? factory, Type pluginType)
        where TPlugin : class
    {
        if (factory is not null)
        {
            var instance = factory();
            return instance ?? throw new InvalidOperationException($"Factory returned null for '{pluginType.FullName}'.");
        }

        var created = Activator.CreateInstance(pluginType);
        if (created is null)
        {
            throw new InvalidOperationException(
                $"Unable to create plugin instance for type '{pluginType.FullName}'. Provide a factory to RegisterSemanticKernelPlugin.");
        }

        return created;
    }
}
