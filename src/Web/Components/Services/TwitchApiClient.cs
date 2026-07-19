namespace Web.Components.Services;

public class TwitchApiClient(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient httpClient = httpClientFactory.CreateClient("twitch");

    public async Task<bool> SendAuthCode(string code, string scopes, string redirectUri)
    {
        var response = await httpClient.PostAsJsonAsync("auth/callback", new AuthCallbackRequest(code, scopes, redirectUri));
        return response.IsSuccessStatusCode;
    }

    // The dashboard polls this frequently and needs to read as "unreachable" within
    // a couple of seconds, not wait out the standard resilience handler's full
    // retry budget (which can take 20+ seconds against a dead dependency).
    public async Task<AuthStatusResponse?> GetAuthStatus()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            return await httpClient.GetFromJsonAsync<AuthStatusResponse>("auth/status", cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }

    // Same reasoning as GetAuthStatus: fail fast toward "unreachable" instead of
    // waiting out the standard resilience handler's retry budget.
    public async Task<StreamStatusResponse?> GetStreamStatus()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            return await httpClient.GetFromJsonAsync<StreamStatusResponse>("stream/status", cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }

    // Same reasoning as GetAuthStatus: fail fast toward "unreachable" instead of
    // waiting out the standard resilience handler's retry budget.
    public async Task<HypeTrainStatusResponse?> GetHypeTrainStatus()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            return await httpClient.GetFromJsonAsync<HypeTrainStatusResponse>("hype-train/status", cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }

    // Same reasoning as GetAuthStatus: fail fast toward "unreachable" instead of
    // waiting out the standard resilience handler's retry budget.
    public async Task<GoalStatusResponse?> GetGoalStatus()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            return await httpClient.GetFromJsonAsync<GoalStatusResponse>("goal/status", cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }

    // Same reasoning as GetAuthStatus: fail fast toward "unreachable" instead of
    // waiting out the standard resilience handler's retry budget.
    public async Task<PollStatusResponse?> GetPollStatus()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            return await httpClient.GetFromJsonAsync<PollStatusResponse>("poll/status", cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }

    // Same reasoning as GetAuthStatus: fail fast toward "unreachable" instead of
    // waiting out the standard resilience handler's retry budget.
    public async Task<PredictionStatusResponse?> GetPredictionStatus()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            return await httpClient.GetFromJsonAsync<PredictionStatusResponse>("prediction/status", cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }

    // Same reasoning as GetAuthStatus: fail fast toward "unreachable" instead of
    // waiting out the standard resilience handler's retry budget.
    public async Task<ActivityFeedItem[]?> GetRecentActivity()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            return await httpClient.GetFromJsonAsync<ActivityFeedItem[]>("activity/recent", cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }

    // Same reasoning as GetAuthStatus: fail fast toward "unreachable" instead of
    // waiting out the standard resilience handler's retry budget.
    public async Task<ModerationLogItem[]?> GetRecentModerationLog()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            return await httpClient.GetFromJsonAsync<ModerationLogItem[]>("moderation/recent", cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }

    // Same reasoning as GetAuthStatus: fail fast toward "unreachable" instead of
    // waiting out the standard resilience handler's retry budget.
    public async Task<RewardItem[]?> GetRewards()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            return await httpClient.GetFromJsonAsync<RewardItem[]>("rewards", cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<CreateRewardResult> CreateReward(CreateRewardRequest request)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("rewards", request);
            if (response.IsSuccessStatusCode)
            {
                var reward = await response.Content.ReadFromJsonAsync<RewardItem>();
                return reward is not null
                    ? new CreateRewardResult(true, reward, null)
                    : new CreateRewardResult(false, null, "Empty response from Twitch service");
            }

            var error = await response.Content.ReadAsStringAsync();
            return new CreateRewardResult(false, null, $"Twitch service returned {(int)response.StatusCode}: {error}");
        }
        catch (HttpRequestException ex)
        {
            return new CreateRewardResult(false, null, $"Could not reach the Twitch service: {ex.Message}");
        }
    }

    public async Task<SetRewardPausedResult> SetRewardPaused(string rewardId, bool isPaused)
    {
        try
        {
            var response = await httpClient.PatchAsJsonAsync($"rewards/{rewardId}/pause", new SetRewardPausedRequest(isPaused));
            if (response.IsSuccessStatusCode)
            {
                var reward = await response.Content.ReadFromJsonAsync<RewardItem>();
                return reward is not null
                    ? new SetRewardPausedResult(true, reward, null)
                    : new SetRewardPausedResult(false, null, "Empty response from Twitch service");
            }

            var error = await response.Content.ReadAsStringAsync();
            return new SetRewardPausedResult(false, null, $"Twitch service returned {(int)response.StatusCode}: {error}");
        }
        catch (HttpRequestException ex)
        {
            return new SetRewardPausedResult(false, null, $"Could not reach the Twitch service: {ex.Message}");
        }
    }

    public async Task<SendChatMessageResponse> SendChatMessage(string message)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("chat/messages", new SendChatMessageRequest(message));
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SendChatMessageResponse>()
                    ?? new SendChatMessageResponse(false, null, "Empty response from Twitch service");
            }

            var error = await response.Content.ReadAsStringAsync();
            return new SendChatMessageResponse(false, null, $"Twitch service returned {(int)response.StatusCode}: {error}");
        }
        catch (HttpRequestException ex)
        {
            return new SendChatMessageResponse(false, null, $"Could not reach the Twitch service: {ex.Message}");
        }
    }
}

public record AuthCallbackRequest(string Code, string Scopes, string RedirectUri);

public record SendChatMessageRequest(string Message, string? ReplyParentMessageId = null);

public record SendChatMessageResponse(bool Sent, string? MessageId, string? DropReason);

public record RoleStatus(string Login, bool NeedsLogin, string[] Scopes);

public record ConnectionStatus(bool Connected, DateTime? LastMessageAtUtc);

public record AuthStatusResponse(RoleStatus? Bot, RoleStatus? Broadcaster, ConnectionStatus BotConnection, ConnectionStatus BroadcasterConnection);

public record StreamStatusResponse(bool? IsLive, DateTimeOffset? StartedAt);

public record HypeTrainStatusResponse(bool IsActive, int Level, int Progress, int Goal);

public record GoalStatusResponse(bool IsActive, string Type, string Description, int CurrentAmount, int TargetAmount);

public record PollChoiceResponse(string Title, int Votes);

public record PollStatusResponse(bool IsActive, string Title, PollChoiceResponse[] Choices);

public record PredictionOutcomeResponse(string Title, string Color, int Users, int ChannelPoints);

public record PredictionStatusResponse(bool IsActive, string Title, bool Locked, PredictionOutcomeResponse[] Outcomes);

public record ActivityFeedItem(string Kind, DateTimeOffset OccurredAt, string DisplayName, string Summary);

public record ModerationLogItem(string Kind, DateTimeOffset OccurredAt, string ModeratorName, string TargetName, string Summary);

public record RewardItem(string Id, string Title, int Cost, string Prompt, bool IsEnabled, bool IsPaused, string BackgroundColor);

public record CreateRewardRequest(
    string Title,
    int Cost,
    string? Prompt = null,
    bool IsEnabled = true,
    string? BackgroundColor = null,
    bool IsUserInputRequired = false,
    bool ShouldRedemptionsSkipRequestQueue = false);

public record CreateRewardResult(bool Success, RewardItem? Reward, string? Error);

public record SetRewardPausedRequest(bool IsPaused);

public record SetRewardPausedResult(bool Success, RewardItem? Reward, string? Error);
