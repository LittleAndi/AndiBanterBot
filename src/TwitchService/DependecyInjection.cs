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
        services.AddSingleton<ITwitchRewardService, TwitchRewardService>();
        services.AddSingleton<ITwitchAdScheduleService, TwitchAdScheduleService>();
        // Two independent WebSocket connections, one per Twitch user identity - see the
        // comment on TwitchWebSocketService for why they can't share a single connection.
        services.AddKeyedSingleton<IWebSocketClient, WebSocketClient>(TwitchUserRole.Bot);
        services.AddKeyedSingleton<IWebSocketClient, WebSocketClient>(TwitchUserRole.Broadcaster);
        services.AddSingleton<ITwitchWebSocketService, TwitchWebSocketService>();
        services.AddHostedService<TwitchEventSubSupervisorService>();
        services.AddHostedService<TwitchActivityChatReactionService>();

        services.AddSingleton<TwitchActivityFeedService>();
        services.AddSingleton<ITwitchActivityFeedService>(sp => sp.GetRequiredService<TwitchActivityFeedService>());
        services.AddHostedService(sp => sp.GetRequiredService<TwitchActivityFeedService>());

        services.AddSingleton<TwitchModerationLogService>();
        services.AddSingleton<ITwitchModerationLogService>(sp => sp.GetRequiredService<TwitchModerationLogService>());
        services.AddHostedService(sp => sp.GetRequiredService<TwitchModerationLogService>());

        return services;
    }
}

