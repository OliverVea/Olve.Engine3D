using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Olve.Engine3D.Input.Actions;

public static class InputActionServiceExtensions
{
    /// <summary>
    /// Registers the actions, their <see cref="InputBindings"/> and <see cref="InputActionManager"/>. Bindings come
    /// from an <see cref="InputBindingOverrides"/> registered by the host, or the actions' defaults if there is none.
    /// </summary>
    public static IServiceCollection AddInputActions(
        this IServiceCollection services,
        IEnumerable<ButtonAction> buttonActions,
        IEnumerable<DirectionalAction> directionalActions)
    {
        foreach (var action in buttonActions)
        {
            services.AddSingleton(action);
        }

        foreach (var action in directionalActions)
        {
            services.AddSingleton(action);
        }

        services.TryAddSingleton(InputBindingOverrides.None);
        services.TryAddSingleton(sp => sp.GetRequiredService<InputBindingOverrides>().Resolve(
            [.. sp.GetServices<ButtonAction>(), .. sp.GetServices<DirectionalAction>()]));
        services.TryAddSingleton<InputActionManager>();
        return services;
    }
}
