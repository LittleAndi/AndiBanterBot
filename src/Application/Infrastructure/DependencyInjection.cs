using System.Net.Http.Headers;
using Microsoft.Extensions.Azure;
using Polly;
using Polly.Extensions.Http;

namespace Application.Infrastructure;
public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTwitch(configuration);
        services.AddOpenAI(configuration);
        services.AddPubgOpenAI(configuration);
    }

    private static IServiceCollection AddTwitch(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddConfigurationOptions<ChatOptions>(configuration);
        // IChatService/IClipService implementations moved to the Helix-based
        // TwitchService project; register adapters here once the feature layer
        // is wired to it.
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
        // services.AddSingleton<IPubgAIClient, PubgAIClient>();
        return services;
    }
}