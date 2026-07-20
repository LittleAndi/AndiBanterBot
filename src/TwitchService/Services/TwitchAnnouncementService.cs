namespace Application.Features.Twitch;

public interface ITwitchAnnouncementService
{
    Task<AnnouncementResult> SendAnnouncementAsync(string message, string? color = null, CancellationToken cancellationToken = default);
}

public record AnnouncementResult(bool Success, string? Error, bool RateLimited = false);

/// <summary>
/// Wraps the Helix Send Chat Announcement endpoint (POST /helix/chat/announcements) as a
/// color-highlighted alternative to plain chat.messages sends, for bot messages that should
/// stand out. Requires a broadcaster user access token with moderator:manage:announcements -
/// same TwitchClientUserAccess + Broadcaster role pattern as TwitchShoutoutService.
/// </summary>
public class TwitchAnnouncementService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ITwitchUserApi twitchUserApi,
    ILogger<TwitchAnnouncementService> logger) : ITwitchAnnouncementService
{
    private static readonly string[] ValidColors = ["blue", "green", "orange", "purple", "primary"];

    private readonly HttpClient twitchHttpClientUserAccess = httpClientFactory.CreateClient("TwitchClientUserAccess");
    private readonly string broadcasterUsername = configuration["Twitch:BroadcasterUsername"] ?? throw new InvalidOperationException("BroadcasterUsername not configured");

    public async Task<AnnouncementResult> SendAnnouncementAsync(string message, string? color = null, CancellationToken cancellationToken = default)
    {
        if (color is not null && !ValidColors.Contains(color, StringComparer.OrdinalIgnoreCase))
        {
            return new AnnouncementResult(false, $"Invalid color '{color}'. Must be one of: {string.Join(", ", ValidColors)}");
        }

        var broadcasterId = await twitchUserApi.GetUserIdAsync(broadcasterUsername, cancellationToken);
        if (broadcasterId is null)
        {
            return new AnnouncementResult(false, $"Could not resolve broadcaster id for {broadcasterUsername}");
        }

        var body = new Dictionary<string, string> { ["message"] = message };
        if (color is not null)
        {
            body["color"] = color;
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            $"helix/chat/announcements?broadcaster_id={broadcasterId}&moderator_id={broadcasterId}")
        {
            Content = JsonContent.Create(body)
        };
        httpRequest.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);

        var response = await twitchHttpClientUserAccess.SendAsync(httpRequest, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning("Announcement was rate limited by Twitch");
            return new AnnouncementResult(false, "Rate limited by Twitch", RateLimited: true);
        }

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Failed to send announcement. Status: {StatusCode}, Response: {Response}", response.StatusCode, content);
            return new AnnouncementResult(false, $"Twitch returned {(int)response.StatusCode}: {content}");
        }

        logger.LogInformation("Sent chat announcement (color: {Color})", color ?? "primary");
        return new AnnouncementResult(true, null);
    }
}
