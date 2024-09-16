namespace Application.Infrastructure;
public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTwitch(configuration);
        services.AddOpenAI(configuration);
    }

    private static IServiceCollection AddTwitch(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddConfigurationOptions<ChatOptions>(configuration, out _);
        services.AddSingleton<IChatService, ChatService>();
        services.AddSingleton<IClipService, ClipService>();
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IPubSubService, PubSubService>();
        services.AddSingleton<IWebsocketService, WebsocketService>();
        services.AddTwitchLibEventSubWebsockets();
        return services;
    }

    private static IServiceCollection AddOpenAI(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddConfigurationOptions<OpenAIClientOptions>(configuration, out _);
        services.AddSingleton<IAIClient, AIClient>();
        services.AddSingleton<IAudioClient, AudioClient>();
        services.AddSingleton<IModerationClient, ModerationClient>();
        return services;
    }
}