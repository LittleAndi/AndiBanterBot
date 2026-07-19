namespace Application.Features.Twitch;

public interface ITwitchRewardService
{
    Task<RewardListResult> GetRewardsAsync(CancellationToken cancellationToken = default);
    Task<RewardOperationResult> CreateRewardAsync(CreateRewardRequest request, CancellationToken cancellationToken = default);
    Task<RewardOperationResult> UpdateRewardAsync(string rewardId, UpdateRewardRequest request, CancellationToken cancellationToken = default);
    Task<RewardOperationResult> SetPausedAsync(string rewardId, bool isPaused, CancellationToken cancellationToken = default);
}

public record CreateRewardRequest(
    string Title,
    int Cost,
    string? Prompt = null,
    bool IsEnabled = true,
    string? BackgroundColor = null,
    bool IsUserInputRequired = false,
    bool ShouldRedemptionsSkipRequestQueue = false);

public record UpdateRewardRequest(
    string? Title = null,
    int? Cost = null,
    string? Prompt = null,
    bool? IsEnabled = null,
    string? BackgroundColor = null,
    bool? IsUserInputRequired = null,
    bool? IsPaused = null,
    bool? ShouldRedemptionsSkipRequestQueue = null);

public record CustomReward(
    string Id,
    string Title,
    int Cost,
    string Prompt,
    bool IsEnabled,
    bool IsPaused,
    string BackgroundColor);

public record RewardOperationResult(bool Success, CustomReward? Reward, string? Error);

public record RewardListResult(bool Success, IReadOnlyList<CustomReward> Rewards, string? Error);

/// <summary>
/// Creates, updates, and pauses/unpauses channel points custom rewards through the Helix
/// Channel Points API (POST/PATCH /helix/channel_points/custom_rewards). Unlike chat send and
/// user lookup, which authenticate via the app-access client, this requires a broadcaster user
/// access token with channel:manage:redemptions - a client-credentials (app access) token can't
/// hold user scopes at all, so this uses TwitchClientUserAccess with the Broadcaster role.
/// </summary>
public class TwitchRewardService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ITwitchUserApi twitchUserApi,
    ILogger<TwitchRewardService> logger) : ITwitchRewardService
{
    private readonly HttpClient twitchHttpClientUserAccess = httpClientFactory.CreateClient("TwitchClientUserAccess");
    private readonly string broadcasterUsername = configuration["Twitch:BroadcasterUsername"] ?? throw new InvalidOperationException("BroadcasterUsername not configured");

    public async Task<RewardListResult> GetRewardsAsync(CancellationToken cancellationToken = default)
    {
        var broadcasterId = await twitchUserApi.GetUserIdAsync(broadcasterUsername, cancellationToken);
        if (broadcasterId is null)
        {
            return new RewardListResult(false, [], $"Could not resolve broadcaster id for {broadcasterUsername}");
        }

        // Twitch refuses to update/pause/delete a reward unless it was created by this app's
        // Client-Id (dashboard-created rewards return 403 on write). Filtering server-side means
        // the UI never lists a reward it can't actually act on.
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"helix/channel_points/custom_rewards?broadcaster_id={broadcasterId}&only_manageable_rewards=true");
        httpRequest.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);

        var response = await twitchHttpClientUserAccess.SendAsync(httpRequest, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to list custom rewards. Status: {StatusCode}, Response: {Response}", response.StatusCode, content);
            return new RewardListResult(false, [], $"Twitch returned {(int)response.StatusCode}: {content}");
        }

        using var doc = JsonDocument.Parse(content);
        var rewards = doc.RootElement.GetProperty("data").EnumerateArray().Select(ParseReward).ToArray();
        return new RewardListResult(true, rewards, null);
    }

    public async Task<RewardOperationResult> CreateRewardAsync(CreateRewardRequest request, CancellationToken cancellationToken = default)
    {
        var broadcasterId = await twitchUserApi.GetUserIdAsync(broadcasterUsername, cancellationToken);
        if (broadcasterId is null)
        {
            return new RewardOperationResult(false, null, $"Could not resolve broadcaster id for {broadcasterUsername}");
        }

        var body = new Dictionary<string, object>
        {
            ["title"] = request.Title,
            ["cost"] = request.Cost,
            ["is_enabled"] = request.IsEnabled,
            ["is_user_input_required"] = request.IsUserInputRequired,
            ["should_redemptions_skip_request_queue"] = request.ShouldRedemptionsSkipRequestQueue,
        };
        if (!string.IsNullOrEmpty(request.Prompt))
        {
            body["prompt"] = request.Prompt;
        }
        if (!string.IsNullOrEmpty(request.BackgroundColor))
        {
            body["background_color"] = request.BackgroundColor;
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"helix/channel_points/custom_rewards?broadcaster_id={broadcasterId}")
        {
            Content = JsonContent.Create(body)
        };
        httpRequest.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);

        return await SendAsync(httpRequest, "create custom reward", cancellationToken);
    }

    public async Task<RewardOperationResult> UpdateRewardAsync(string rewardId, UpdateRewardRequest request, CancellationToken cancellationToken = default)
    {
        var broadcasterId = await twitchUserApi.GetUserIdAsync(broadcasterUsername, cancellationToken);
        if (broadcasterId is null)
        {
            return new RewardOperationResult(false, null, $"Could not resolve broadcaster id for {broadcasterUsername}");
        }

        var body = BuildUpdateBody(request);
        if (body.Count == 0)
        {
            return new RewardOperationResult(false, null, "No fields to update");
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"helix/channel_points/custom_rewards?broadcaster_id={broadcasterId}&id={rewardId}")
        {
            Content = JsonContent.Create(body)
        };
        httpRequest.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);

        return await SendAsync(httpRequest, "update custom reward", cancellationToken);
    }

    public Task<RewardOperationResult> SetPausedAsync(string rewardId, bool isPaused, CancellationToken cancellationToken = default) =>
        UpdateRewardAsync(rewardId, new UpdateRewardRequest(IsPaused: isPaused), cancellationToken);

    private static Dictionary<string, object> BuildUpdateBody(UpdateRewardRequest request)
    {
        var body = new Dictionary<string, object>();
        if (request.Title is not null) body["title"] = request.Title;
        if (request.Cost is not null) body["cost"] = request.Cost.Value;
        if (request.Prompt is not null) body["prompt"] = request.Prompt;
        if (request.IsEnabled is not null) body["is_enabled"] = request.IsEnabled.Value;
        if (request.BackgroundColor is not null) body["background_color"] = request.BackgroundColor;
        if (request.IsUserInputRequired is not null) body["is_user_input_required"] = request.IsUserInputRequired.Value;
        if (request.IsPaused is not null) body["is_paused"] = request.IsPaused.Value;
        if (request.ShouldRedemptionsSkipRequestQueue is not null) body["should_redemptions_skip_request_queue"] = request.ShouldRedemptionsSkipRequestQueue.Value;
        return body;
    }

    private async Task<RewardOperationResult> SendAsync(HttpRequestMessage httpRequest, string action, CancellationToken cancellationToken)
    {
        var response = await twitchHttpClientUserAccess.SendAsync(httpRequest, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to {Action}. Status: {StatusCode}, Response: {Response}", action, response.StatusCode, content);
            return new RewardOperationResult(false, null, $"Twitch returned {(int)response.StatusCode}: {content}");
        }

        using var doc = JsonDocument.Parse(content);
        var data = doc.RootElement.GetProperty("data")[0];
        var reward = ParseReward(data);

        logger.LogInformation("Successfully performed '{Action}' for reward {RewardId} ({Title})", action, reward.Id, reward.Title);
        return new RewardOperationResult(true, reward, null);
    }

    private static CustomReward ParseReward(JsonElement data) => new(
        data.GetProperty("id").GetString()!,
        data.GetProperty("title").GetString()!,
        data.GetProperty("cost").GetInt32(),
        data.TryGetProperty("prompt", out var prompt) ? prompt.GetString() ?? string.Empty : string.Empty,
        data.GetProperty("is_enabled").GetBoolean(),
        data.GetProperty("is_paused").GetBoolean(),
        data.TryGetProperty("background_color", out var color) ? color.GetString() ?? string.Empty : string.Empty);
}
