using System.Net.Http.Headers;

namespace Application.Infrastructure;
public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTwitch(configuration);
        services.AddOpenAI(configuration);
        services.AddPubgOpenAI(configuration);
        services.AddPubg(configuration);
    }

    private static IServiceCollection AddTwitch(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddConfigurationOptions<ChatOptions>(configuration);
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
        services.AddConfigurationOptions<OpenAI.OpenAIClientOptions>(configuration);
        services.AddSingleton<IAIClient, AIClient>();
        services.AddSingleton<IAudioClient, OpenAI.AudioClient>();
        services.AddSingleton<IModerationClient, ModerationClient>();
        services.AddSingleton<IAssistantClient, OpenAI.AssistantClient>();
        return services;
    }

    private static IServiceCollection AddPubgOpenAI(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddConfigurationOptions<PubgOpenAIClientOptions>(configuration);
        services.AddSingleton<IPubgAIClient, PubgAIClient>();
        return services;
    }

    private static IServiceCollection AddPubg(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddConfigurationOptions<PubgClientOptions>(configuration);
        services.AddHttpClient("pubg", (ServiceProvider, client) =>
        {
            var pubgClientOptions = ServiceProvider.GetRequiredService<IOptionsMonitor<PubgClientOptions>>().CurrentValue;
            client.BaseAddress = new Uri(pubgClientOptions.BaseAddress);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pubgClientOptions.ApiKey);
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.api+json");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");

        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler() { AutomaticDecompression = System.Net.DecompressionMethods.GZip });
        services.AddTransient<IPubgApiClient, PubgApiClient>();
        return services;
    }

}