namespace Web.Components.Services;

public class TwitchApiClient(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient httpClient = httpClientFactory.CreateClient("twitch");

    public async Task<bool> SendAuthCode(string code, string scopes, string redirectUri)
    {
        var response = await httpClient.PostAsJsonAsync("auth/callback", new AuthCallbackRequest(code, scopes, redirectUri));
        return response.IsSuccessStatusCode;
    }

    public async Task<AuthStatusResponse?> GetAuthStatus()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<AuthStatusResponse>("auth/status");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

public record AuthCallbackRequest(string Code, string Scopes, string RedirectUri);

public record RoleStatus(string Login, bool NeedsLogin, string[] Scopes);

public record AuthStatusResponse(RoleStatus? Bot, RoleStatus? Broadcaster, bool WebSocketConnected, DateTime? LastMessageAtUtc);
