using System.Net.Http.Headers;
using Application.Infrastructure.Pubg;

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

    private static IServiceCollection AddPubgOpenAI(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddConfigurationOptions<PubgOpenAIClientOptions>(configuration, out _);
        services.AddSingleton<IPubgAIClient, PubgAIClient>();
        return services;
    }

    private static IServiceCollection AddPubg(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddConfigurationOptions<PubgClientOptions>(configuration, out var pubgClientOptions);
        services.AddHttpClient("pubg", (ServiceProvider, client) =>
        {
            client.BaseAddress = new Uri(pubgClientOptions.BaseAddress);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pubgClientOptions.ApiKey);
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.api+json");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");

        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler() { AutomaticDecompression = System.Net.DecompressionMethods.GZip });
        services.AddTransient<IPubgApiClient, PubgApiClient>();
        return services;
    }

}