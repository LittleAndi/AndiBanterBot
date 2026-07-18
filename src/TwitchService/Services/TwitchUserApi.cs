using System.Collections.Concurrent;

namespace Application.Features.Twitch;

public interface ITwitchUserApi
{
    Task<string?> GetUserIdAsync(string login, CancellationToken cancellationToken = default);
}

public class TwitchUserApi(IHttpClientFactory httpClientFactory, ILogger<TwitchUserApi> logger) : ITwitchUserApi
{
    private readonly HttpClient twitchHttpClientAppAccess = httpClientFactory.CreateClient("TwitchClientAppAccess");
    private readonly ConcurrentDictionary<string, string> userIdsByLogin = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string?> GetUserIdAsync(string login, CancellationToken cancellationToken = default)
    {
        if (userIdsByLogin.TryGetValue(login, out var cached))
        {
            return cached;
        }

        try
        {
            var response = await twitchHttpClientAppAccess.GetAsync($"helix/users?login={login}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to get user ID for {Login}. Status: {StatusCode}", login, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);
            var users = doc.RootElement.GetProperty("data");

            if (users.GetArrayLength() == 0)
            {
                logger.LogError("User {Login} not found", login);
                return null;
            }

            var userId = users[0].GetProperty("id").GetString();
            if (userId is null)
            {
                return null;
            }

            logger.LogInformation("Retrieved user ID {UserId} for username {Login}", userId, login);
            userIdsByLogin[login] = userId;
            return userId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user ID for username {Login}", login);
            return null;
        }
    }
}
