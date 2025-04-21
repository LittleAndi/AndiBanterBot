namespace Application;
public static class DependencyInjection
{
    public static IServiceCollection AddPubg(this IServiceCollection services, IConfiguration configuration)
    {
        var pubgClientOptionsSection = configuration.GetSection(PubgClientOptions.SectionName);
        var pubgClientOptions = pubgClientOptionsSection.Get<PubgClientOptions>()!;
        services.Configure<PubgClientOptions>(pubgClientOptionsSection);
        services.AddHttpClient("pubg", (ServiceProvider, client) =>
        {
            client.BaseAddress = new Uri(pubgClientOptions.BaseAddress);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pubgClientOptions.ApiKey);
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.api+json");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");

        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler() { AutomaticDecompression = System.Net.DecompressionMethods.GZip })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(GetRetryPolicy());

        services.AddAzureClients((builder) =>
        {
            builder.AddBlobServiceClient(pubgClientOptions.Storage).WithName("pubgStorage");
        });
        services.AddSingleton<IPubgStorageClient, PubgStorageClient>();
        services.AddTransient<IPubgApiClient, PubgApiClient>();
        return services;
    }

    private static Polly.Retry.AsyncRetryPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}