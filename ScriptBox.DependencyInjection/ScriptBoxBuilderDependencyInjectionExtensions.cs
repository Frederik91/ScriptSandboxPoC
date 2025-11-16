using Microsoft.Extensions.DependencyInjection;
using ScriptBox;

namespace ScriptBox.DependencyInjection;

public static class ScriptBoxBuilderDependencyInjectionExtensions
{
    public static ScriptBoxBuilder UseMicrosoftDependencyInjection(
        this ScriptBoxBuilder builder,
        IServiceProvider serviceProvider)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (serviceProvider is null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        return builder.UseApiFactory(type =>
            ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, type));
    }
}
