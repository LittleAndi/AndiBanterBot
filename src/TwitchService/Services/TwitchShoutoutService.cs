namespace Application.Features.Twitch;

public interface ITwitchShoutoutService
{
    Task<ShoutoutResult> SendShoutoutAsync(string toBroadcasterLogin, CancellationToken cancellationToken = default);
}

public record ShoutoutResult(bool Success, string? Error, bool RateLimited = false);

/// <summary>
/// Wraps the Helix Send a Shoutout endpoint (POST /helix/chat/shoutouts), which requires
/// a broadcaster user access token with moderator:manage:shoutouts - same TwitchClientUserAccess
/// + Broadcaster role pattern as TwitchAdScheduleService. The broadcaster is a moderator of
/// their own channel, so their own user id satisfies both from_broadcaster_id and moderator_id.
/// </summary>
public class TwitchShoutoutService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ITwitchUserApi twitchUserApi,
    ILogger<TwitchShoutoutService> logger) : ITwitchShoutoutService
{
    private readonly HttpClient twitchHttpClientUserAccess = httpClientFactory.CreateClient("TwitchClientUserAccess");
    private readonly string broadcasterUsername = configuration["Twitch:BroadcasterUsername"] ?? throw new InvalidOperationException("BroadcasterUsername not configured");

    public async Task<ShoutoutResult> SendShoutoutAsync(string toBroadcasterLogin, CancellationToken cancellationToken = default)
    {
        var broadcasterId = await twitchUserApi.GetUserIdAsync(broadcasterUsername, cancellationToken);
        if (broadcasterId is null)
        {
            return new ShoutoutResult(false, $"Could not resolve broadcaster id for {broadcasterUsername}");
        }

        var toBroadcasterId = await twitchUserApi.GetUserIdAsync(toBroadcasterLogin, cancellationToken);
        if (toBroadcasterId is null)
        {
            return new ShoutoutResult(false, $"Could not resolve user id for {toBroadcasterLogin}");
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            $"helix/chat/shoutouts?from_broadcaster_id={broadcasterId}&to_broadcaster_id={toBroadcasterId}&moderator_id={broadcasterId}");
        httpRequest.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);

        var response = await twitchHttpClientUserAccess.SendAsync(httpRequest, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning("Shoutout to {ToBroadcasterLogin} was rate limited by Twitch", toBroadcasterLogin);
            return new ShoutoutResult(false, "Rate limited by Twitch", RateLimited: true);
        }

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Failed to send shoutout to {ToBroadcasterLogin}. Status: {StatusCode}, Response: {Response}", toBroadcasterLogin, response.StatusCode, content);
            return new ShoutoutResult(false, $"Twitch returned {(int)response.StatusCode}: {content}");
        }

        logger.LogInformation("Sent shoutout to {ToBroadcasterLogin}", toBroadcasterLogin);
        return new ShoutoutResult(true, null);
    }
}
