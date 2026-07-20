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
            ParseInt(data, "duration"),
            ParseInt(data, "preroll_free_time"),
            ParseInt(data, "snooze_count"),
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
            ParseInt(data, "snooze_count"),
            ParseTimestamp(data, "snooze_refresh_at"),
            ParseTimestamp(data, "next_ad_at"));

        logger.LogInformation("Snoozed next ad, {SnoozeCount} snooze(s) remaining, next ad at {NextAdAt}", snooze.SnoozeCount, snooze.NextAdAt);
        return new SnoozeAdResult(true, snooze, null);
    }

    // Twitch's own docs example for this endpoint shows "duration"/"snooze_count" as JSON
    // strings while the live API has been observed returning ad-schedule timestamps as Unix
    // epoch numbers instead of the documented RFC3339 strings - the documented type doesn't
    // reliably match what's on the wire, so both fields below tolerate either JSON shape.
    // An empty string or a non-positive epoch means the field doesn't apply yet (e.g.
    // next_ad_at with nothing scheduled, last_ad_at before any ad has run).
    private static DateTimeOffset? ParseTimestamp(JsonElement data, string propertyName)
    {
        if (!data.TryGetProperty(propertyName, out var el))
        {
            return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrEmpty(el.GetString()) ? null : DateTimeOffset.Parse(el.GetString()!),
            JsonValueKind.Number => el.GetInt64() is var seconds && seconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null,
            _ => null,
        };
    }

    private static int ParseInt(JsonElement data, string propertyName)
    {
        if (!data.TryGetProperty(propertyName, out var el))
        {
            return 0;
        }

        return el.ValueKind switch
        {
            JsonValueKind.Number => el.GetInt32(),
            JsonValueKind.String => int.TryParse(el.GetString(), out var parsed) ? parsed : 0,
            _ => 0,
        };
    }
}
