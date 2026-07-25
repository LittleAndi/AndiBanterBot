using OverlayService.Overlays;
using OverlayService.Overlays.Starting;

namespace OverlayService;

public static class DependencyInjection
{
    public static IServiceCollection AddOverlays(this IServiceCollection services)
    {
        services.AddSingleton<IOverlayRegistry, OverlayRegistry>();
        services.AddSingleton<IStartingCountdownStore, StartingCountdownStore>();

        services.AddHttpClient("twitch", client =>
        {
            client.BaseAddress = new("https+http://twitch");
        });
        services.AddHttpClient("pubg", client => client.BaseAddress = new("https+http://pubg"));

        return services;
    }
}
