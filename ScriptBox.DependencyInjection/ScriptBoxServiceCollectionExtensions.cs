using Microsoft.Extensions.DependencyInjection;
using ScriptBox;

namespace ScriptBox.DependencyInjection;

public static class ScriptBoxServiceCollectionExtensions
{
    public static IServiceCollection AddScriptBox(
        this IServiceCollection services,
        Action<IScriptBoxConfigurator, IServiceProvider>? configure = null)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddSingleton<IScriptBox>(sp =>
        {
            var builder = ScriptBoxBuilder
                .Create()
                .UseMicrosoftDependencyInjection(sp);

            configure?.Invoke(builder, sp);

            return builder.Build();
        });

        return services;
    }
}
