namespace Application.Features.Twitch;

public interface ITwitchClipService
{
    Task<CreateClipResult> CreateClipAsync(CancellationToken cancellationToken = default);
    Task<ClipListResult> GetRecentClipsAsync(int count = 20, CancellationToken cancellationToken = default);
}

public record CreateClipResult(bool Success, string? ClipId, string? EditUrl, string? Error);

public record Clip(
    string Id,
    string Url,
    string EmbedUrl,
    string BroadcasterName,
    string CreatorName,
    string Title,
    int ViewCount,
    DateTimeOffset CreatedAt,
    string ThumbnailUrl,
    int DurationSeconds);

public record ClipListResult(bool Success, IReadOnlyList<Clip> Clips, string? Error);

/// <summary>
/// Wraps the Helix Create Clip (POST /helix/clips) and Get Clips (GET /helix/clips) endpoints.
/// Create Clip requires a broadcaster user access token with clips:edit - a client-credentials
/// (app access) token can't hold user scopes at all - so this uses TwitchClientUserAccess with
/// the Broadcaster role, same as TwitchRewardService/TwitchShoutoutService. Get Clips has no
/// scope requirement and would work on either client; kept on the same user-access client for
/// consistency rather than splitting the two calls across clients.
/// </summary>
public class TwitchClipService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ITwitchUserApi twitchUserApi,
    ILogger<TwitchClipService> logger) : ITwitchClipService
{
    private readonly HttpClient twitchHttpClientUserAccess = httpClientFactory.CreateClient("TwitchClientUserAccess");
    private readonly string broadcasterUsername = configuration["Twitch:BroadcasterUsername"] ?? throw new InvalidOperationException("BroadcasterUsername not configured");

    public async Task<CreateClipResult> CreateClipAsync(CancellationToken cancellationToken = default)
    {
        var broadcasterId = await twitchUserApi.GetUserIdAsync(broadcasterUsername, cancellationToken);
        if (broadcasterId is null)
        {
            return new CreateClipResult(false, null, null, $"Could not resolve broadcaster id for {broadcasterUsername}");
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"helix/clips?broadcaster_id={broadcasterId}");
        httpRequest.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);

        var response = await twitchHttpClientUserAccess.SendAsync(httpRequest, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to create clip. Status: {StatusCode}, Response: {Response}", response.StatusCode, content);
            return new CreateClipResult(false, null, null, $"Twitch returned {(int)response.StatusCode}: {content}");
        }

        using var doc = JsonDocument.Parse(content);
        var data = doc.RootElement.GetProperty("data")[0];
        var clipId = data.GetProperty("id").GetString();
        var editUrl = data.GetProperty("edit_url").GetString();

        logger.LogInformation("Created clip {ClipId}", clipId);
        return new CreateClipResult(true, clipId, editUrl, null);
    }

    public async Task<ClipListResult> GetRecentClipsAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        var broadcasterId = await twitchUserApi.GetUserIdAsync(broadcasterUsername, cancellationToken);
        if (broadcasterId is null)
        {
            return new ClipListResult(false, [], $"Could not resolve broadcaster id for {broadcasterUsername}");
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"helix/clips?broadcaster_id={broadcasterId}&first={count}");
        httpRequest.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);

        var response = await twitchHttpClientUserAccess.SendAsync(httpRequest, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to list clips. Status: {StatusCode}, Response: {Response}", response.StatusCode, content);
            return new ClipListResult(false, [], $"Twitch returned {(int)response.StatusCode}: {content}");
        }

        using var doc = JsonDocument.Parse(content);
        // Twitch orders by view count, not recency, when queried by broadcaster_id with no date
        // filter. Re-sort by created_at so the panel actually shows the newest clips first.
        var clips = doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(ParseClip)
            .OrderByDescending(c => c.CreatedAt)
            .ToArray();
        return new ClipListResult(true, clips, null);
    }

    private static Clip ParseClip(JsonElement data) => new(
        data.GetProperty("id").GetString()!,
        data.GetProperty("url").GetString()!,
        data.GetProperty("embed_url").GetString()!,
        data.GetProperty("broadcaster_name").GetString()!,
        data.GetProperty("creator_name").GetString()!,
        data.GetProperty("title").GetString()!,
        data.GetProperty("view_count").GetInt32(),
        data.GetProperty("created_at").GetDateTimeOffset(),
        data.GetProperty("thumbnail_url").GetString()!,
        (int)data.GetProperty("duration").GetDouble());
}
