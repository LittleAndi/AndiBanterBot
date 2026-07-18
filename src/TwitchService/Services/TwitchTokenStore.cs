namespace Application.Features.Twitch;

public enum TwitchUserRole
{
    Bot,
    Broadcaster
}

public record TwitchTokenInfo(TwitchUserRole Role, string Login, string UserId);

public interface ITwitchTokenStore
{
    Task<TwitchTokenInfo> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default);
    Task<string?> GetAccessTokenAsync(TwitchUserRole role, CancellationToken cancellationToken = default);
    Task<string?> RefreshAsync(TwitchUserRole role, CancellationToken cancellationToken = default);
    bool HasToken(TwitchUserRole role);
}

public class TwitchTokenStore : ITwitchTokenStore
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConfiguration configuration;
    private readonly ILogger<TwitchTokenStore> logger;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string storePath;
    private readonly Dictionary<TwitchUserRole, StoredToken> tokens;

    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(5);

    public TwitchTokenStore(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<TwitchTokenStore> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
        this.logger = logger;

        storePath = configuration["Twitch:TokenStorePath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AndiBanterBot", "twitch-tokens.json");
        tokens = Load();
    }

    public bool HasToken(TwitchUserRole role)
    {
        lock (tokens)
        {
            return tokens.TryGetValue(role, out var token) && !string.IsNullOrEmpty(token.RefreshToken);
        }
    }

    public async Task<TwitchTokenInfo> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        var clientId = configuration["Twitch:ClientId"]!;
        var clientSecret = configuration["Twitch:ClientSecret"]!;

        using var client = CreateAuthClient();
        var response = await client.PostAsync("token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            }), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Token exchange failed. Status: {StatusCode}, Response: {Response}", response.StatusCode, error);
            throw new InvalidOperationException($"Twitch token exchange failed with status {response.StatusCode}");
        }

        var result = (await response.Content.ReadFromJsonAsync<TwitchTokenResponse>(cancellationToken: cancellationToken))!;

        var validation = await ValidateAsync(client, result.AccessToken, cancellationToken);
        var role = ResolveRole(validation.Login);

        var stored = new StoredToken
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken ?? string.Empty,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(result.ExpiresIn),
            Login = validation.Login,
            UserId = validation.UserId,
            Scopes = validation.Scopes
        };

        lock (tokens)
        {
            tokens[role] = stored;
            Save();
        }

        logger.LogInformation("Stored {Role} token for {Login} ({UserId}), expires {ExpiresAtUtc:u}, scopes: {Scopes}",
            role, validation.Login, validation.UserId, stored.ExpiresAtUtc, string.Join(' ', validation.Scopes));

        return new TwitchTokenInfo(role, validation.Login, validation.UserId);
    }

    public async Task<string?> GetAccessTokenAsync(TwitchUserRole role, CancellationToken cancellationToken = default)
    {
        StoredToken? token;
        lock (tokens)
        {
            tokens.TryGetValue(role, out token);
        }

        if (token is null) return null;

        if (DateTime.UtcNow < token.ExpiresAtUtc - ExpiryMargin)
        {
            return token.AccessToken;
        }

        return await RefreshAsync(role, cancellationToken);
    }

    public async Task<string?> RefreshAsync(TwitchUserRole role, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            StoredToken? token;
            lock (tokens)
            {
                tokens.TryGetValue(role, out token);
            }

            if (token is null || string.IsNullOrEmpty(token.RefreshToken))
            {
                logger.LogWarning("No refresh token stored for {Role}; a new browser login is required", role);
                return null;
            }

            // Another caller may have refreshed while we waited on the gate
            if (DateTime.UtcNow < token.ExpiresAtUtc - ExpiryMargin)
            {
                return token.AccessToken;
            }

            var clientId = configuration["Twitch:ClientId"]!;
            var clientSecret = configuration["Twitch:ClientSecret"]!;

            using var client = CreateAuthClient();
            var response = await client.PostAsync("token", new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = token.RefreshToken
                }), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Refresh of {Role} token failed; a new browser login is required. Status: {StatusCode}, Response: {Response}",
                    role, response.StatusCode, error);
                return null;
            }

            var result = (await response.Content.ReadFromJsonAsync<TwitchTokenResponse>(cancellationToken: cancellationToken))!;

            lock (tokens)
            {
                token.AccessToken = result.AccessToken;
                token.RefreshToken = result.RefreshToken ?? token.RefreshToken;
                token.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(result.ExpiresIn);
                Save();
            }

            logger.LogInformation("Refreshed {Role} token, expires {ExpiresAtUtc:u}", role, token.ExpiresAtUtc);
            return result.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<TwitchValidateResponse> ValidateAsync(HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "validate");
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TwitchValidateResponse>(cancellationToken: cancellationToken))!;
    }

    private TwitchUserRole ResolveRole(string login)
    {
        var broadcasterUsername = configuration["Twitch:BroadcasterUsername"];
        return string.Equals(login, broadcasterUsername, StringComparison.OrdinalIgnoreCase)
            ? TwitchUserRole.Broadcaster
            : TwitchUserRole.Bot;
    }

    private HttpClient CreateAuthClient() => httpClientFactory.CreateClient("TwitchAuth");

    private Dictionary<TwitchUserRole, StoredToken> Load()
    {
        try
        {
            if (File.Exists(storePath))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<TwitchUserRole, StoredToken>>(File.ReadAllText(storePath));
                if (loaded is not null)
                {
                    logger.LogInformation("Loaded {Count} stored Twitch token(s) from {StorePath}", loaded.Count, storePath);
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load token store from {StorePath}, starting empty", storePath);
        }

        return [];
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(storePath)!);
            File.WriteAllText(storePath, JsonSerializer.Serialize(tokens, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not persist token store to {StorePath}", storePath);
        }
    }

    private sealed class StoredToken
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public string Login { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string[] Scopes { get; set; } = [];
    }
}
