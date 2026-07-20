namespace Application.Features.Twitch;

public interface ITwitchAdScheduleService
{
    Task<AdScheduleResult> GetAdScheduleAsync(CancellationToken cancellationToken = default);
    Task<SnoozeAdResult> SnoozeNextAdAsync(CancellationToken cancellationToken = default);
}

public record AdSchedule(
    DateTimeOffset? NextAdAt,
    DateTimeOffset? LastAdAt,
    int DurationSeconds,
    int PrerollFreeTimeSeconds,
    int SnoozeCount,
    DateTimeOffset? SnoozeRefreshAt);

public record AdScheduleResult(bool Success, AdSchedule? Schedule, string? Error);

public record AdSnooze(int SnoozeCount, DateTimeOffset? SnoozeRefreshAt, DateTimeOffset? NextAdAt);

public record SnoozeAdResult(bool Success, AdSnooze? Snooze, string? Error);

/// <summary>
/// Wraps the Helix Get Ad Schedule (GET /helix/channels/ads) and Snooze Next Ad
/// (POST /helix/channels/ads/schedule/snooze) endpoints. Both require a broadcaster user
/// access token - Get needs channel:read:ads, Snooze needs channel:manage:ads - so this uses
/// TwitchClientUserAccess with the Broadcaster role, same as TwitchRewardService.
/// </summary>
public class TwitchAdScheduleService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ITwitchUserApi twitchUserApi,
    ILogger<TwitchAdScheduleService> logger) : ITwitchAdScheduleService
{
    private readonly HttpClient twitchHttpClientUserAccess = httpClientFactory.CreateClient("TwitchClientUserAccess");
    private readonly string broadcasterUsername = configuration["Twitch:BroadcasterUsername"] ?? throw new InvalidOperationException("BroadcasterUsername not configured");

    public async Task<AdScheduleResult> GetAdScheduleAsync(CancellationToken cancellationToken = default)
    {
        var broadcasterId = await twitchUserApi.GetUserIdAsync(broadcasterUsername, cancellationToken);
        if (broadcasterId is null)
        {
            return new AdScheduleResult(false, null, $"Could not resolve broadcaster id for {broadcasterUsername}");
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"helix/channels/ads?broadcaster_id={broadcasterId}");
        httpRequest.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);

        var response = await twitchHttpClientUserAccess.SendAsync(httpRequest, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to get ad schedule. Status: {StatusCode}, Response: {Response}", response.StatusCode, content);
            return new AdScheduleResult(false, null, $"Twitch returned {(int)response.StatusCode}: {content}");
        }

        using var doc = JsonDocument.Parse(content);
        var data = doc.RootElement.GetProperty("data")[0];

        var schedule = new AdSchedule(
            ParseTimestamp(data, "next_ad_at"),
            ParseTimestamp(data, "last_ad_at"),
            data.GetProperty("duration").GetInt32(),
            data.GetProperty("preroll_free_time").GetInt32(),
            data.GetProperty("snooze_count").GetInt32(),
            ParseTimestamp(data, "snooze_refresh_at"));

        return new AdScheduleResult(true, schedule, null);
    }

    public async Task<SnoozeAdResult> SnoozeNextAdAsync(CancellationToken cancellationToken = default)
    {
        var broadcasterId = await twitchUserApi.GetUserIdAsync(broadcasterUsername, cancellationToken);
        if (broadcasterId is null)
        {
            return new SnoozeAdResult(false, null, $"Could not resolve broadcaster id for {broadcasterUsername}");
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"helix/channels/ads/schedule/snooze?broadcaster_id={broadcasterId}");
        httpRequest.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);

        var response = await twitchHttpClientUserAccess.SendAsync(httpRequest, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to snooze next ad. Status: {StatusCode}, Response: {Response}", response.StatusCode, content);
            return new SnoozeAdResult(false, null, $"Twitch returned {(int)response.StatusCode}: {content}");
        }

        using var doc = JsonDocument.Parse(content);
        var data = doc.RootElement.GetProperty("data")[0];

        var snooze = new AdSnooze(
            data.GetProperty("snooze_count").GetInt32(),
            ParseTimestamp(data, "snooze_refresh_at"),
            ParseTimestamp(data, "next_ad_at"));

        logger.LogInformation("Snoozed next ad, {SnoozeCount} snooze(s) remaining, next ad at {NextAdAt}", snooze.SnoozeCount, snooze.NextAdAt);
        return new SnoozeAdResult(true, snooze, null);
    }

    // Twitch returns an empty string for timestamp fields that don't apply yet
    // (e.g. next_ad_at with nothing scheduled, last_ad_at before any ad has run).
    private static DateTimeOffset? ParseTimestamp(JsonElement data, string propertyName)
    {
        var value = data.TryGetProperty(propertyName, out var el) ? el.GetString() : null;
        return string.IsNullOrEmpty(value) ? null : DateTimeOffset.Parse(value);
    }
}
