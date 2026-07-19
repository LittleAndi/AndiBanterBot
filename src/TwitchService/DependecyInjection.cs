namespace Application.Features.Twitch;

public static class DependencyInjection
{
    public static IServiceCollection AddTwitch(this IServiceCollection services)
    {
        services.AddTransient<TwitchAppAuthHandler>();
        services.AddTransient<TwitchUserAuthHandler>();
        services.AddSingleton<ITwitchTokenStore, TwitchTokenStore>();

        services.AddHttpClient("TwitchAuth", (configure) =>
            {
                configure.BaseAddress = new Uri("https://id.twitch.tv/oauth2/");
            });

        services.AddHttpClient("TwitchClientAppAccess", (configure) =>
            {
                configure.BaseAddress = new Uri("https://api.twitch.tv");
            })
            .AddHttpMessageHandler<TwitchAppAuthHandler>();

        services.AddHttpClient("TwitchClientUserAccess", (configure) =>
            {
                configure.BaseAddress = new Uri("https://api.twitch.tv");
            })
            .AddHttpMessageHandler<TwitchUserAuthHandler>();

        services.AddSingleton<ITwitchUserApi, TwitchUserApi>();
        services.AddSingleton<ITwitchChatService, TwitchChatService>();
        services.AddSingleton<IWebSocketClient, WebSocketClient>();
        services.AddSingleton<ITwitchWebSocketService, TwitchWebSocketService>();
        services.AddHostedService<TwitchEventSubSupervisorService>();
        services.AddHostedService<TwitchActivityChatReactionService>();

        services.AddSingleton<TwitchActivityFeedService>();
        services.AddSingleton<ITwitchActivityFeedService>(sp => sp.GetRequiredService<TwitchActivityFeedService>());
        services.AddHostedService(sp => sp.GetRequiredService<TwitchActivityFeedService>());

        return services;
    }
}

