namespace Application.Features.Twitch;

public class TwitchUserAuthHandler(ITwitchTokenStore tokenStore, IConfiguration configuration, ILogger<TwitchUserAuthHandler> logger) : DelegatingHandler
{
    private readonly string clientId = configuration["Twitch:ClientId"]!;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!request.Options.TryGetValue(HttpRequestOptionKeys.UserRole, out var role))
        {
            role = TwitchUserRole.Bot;
        }

        var token = await tokenStore.GetAccessTokenAsync(role, cancellationToken);
        if (token is null)
        {
            logger.LogWarning("No {Role} user token available, request to {Uri} will be sent without user access token", role, request.RequestUri);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Client-Id", clientId);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && token is not null)
        {
            logger.LogWarning("Received 401 for {Role} request, refreshing token and retrying", role);
            token = await tokenStore.RefreshAsync(role, cancellationToken);
            if (token is not null)
            {
                response.Dispose();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                response = await base.SendAsync(request, cancellationToken);
            }
        }

        return response;
    }
}
