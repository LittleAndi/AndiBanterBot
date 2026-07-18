namespace Application.Features.Twitch;

public class TwitchAppAuthHandler(IConfiguration configuration, ILogger<TwitchAppAuthHandler> logger) : DelegatingHandler
{
    private string? cachedToken;
    private DateTime tokenExpiry;
    private readonly string clientId = configuration["Twitch:ClientId"]!;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (NeedsNewToken())
        {
            await GetNewTokenAsync(cancellationToken);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cachedToken);
        request.Headers.Add("Client-Id", clientId);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            logger.LogWarning("Token expired during request, getting new token");
            await GetNewTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cachedToken);
            response = await base.SendAsync(request, cancellationToken);
        }

        return response;
    }

    private bool NeedsNewToken()
    {
        return string.IsNullOrEmpty(cachedToken) || DateTime.UtcNow >= tokenExpiry;
    }

    private async Task GetNewTokenAsync(CancellationToken cancellationToken)
    {
        var clientId = configuration["Twitch:ClientId"];
        var clientSecret = configuration["Twitch:ClientSecret"];

        using var client = new HttpClient
        {
            BaseAddress = new Uri("https://id.twitch.tv/oauth2/")
        };

        var response = await client.PostAsync("token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = clientId!,
                ["client_secret"] = clientSecret!,
                ["grant_type"] = "client_credentials",
                ["scope"] = "channel:read:subscriptions channel:manage:broadcast channel:manage:moderators chat:read chat:edit"
            }), cancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TwitchTokenResponse>(cancellationToken: cancellationToken);

        cachedToken = result!.AccessToken;
        tokenExpiry = DateTime.UtcNow.AddSeconds(result.ExpiresIn - 300); // Buffer of 5 minutes

        logger.LogInformation("New Twitch access token obtained, expires in {ExpiresIn} seconds", result.ExpiresIn);
    }
}
