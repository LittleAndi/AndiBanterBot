using OverlayService.Overlays;

namespace OverlayService;

public static class DependencyInjection
{
    public static IServiceCollection AddOverlays(this IServiceCollection services)
    {
        services.AddSingleton<IOverlayRegistry, OverlayRegistry>();

        services.AddHttpClient("pubg", client => client.BaseAddress = new("https+http://pubg"));

        return services;
    }
}
