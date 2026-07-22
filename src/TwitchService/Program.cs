var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddTwitch();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("auth/callback", async (
    AuthCallbackRequest request,
    ITwitchTokenStore tokenStore,
    ITwitchWebSocketService twitchWebSocketService,
    ILogger<Program> logger) =>
{
    TwitchTokenInfo info;
    try
    {
        info = await tokenStore.ExchangeCodeAsync(request.Code, request.RedirectUri);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }

    // Connects and returns; no-op if that role's websocket is already connecting or connected
    try
    {
        await twitchWebSocketService.Start(info.Role);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Token stored, but starting the EventSub websocket failed");
        return Results.Problem("Token stored, but starting the EventSub websocket failed", statusCode: StatusCodes.Status502BadGateway);
    }

    if (info.Role == TwitchUserRole.Broadcaster)
    {
        await twitchWebSocketService.SubscribeToBroadcasterSubscriptions();
    }

    logger.LogInformation("Auth callback processed for {Login} as {Role}", info.Login, info.Role);
    return Results.Ok(new AuthCallbackResponse(info.Role.ToString(), info.Login));
});

app.MapGet("auth/status", (ITwitchTokenStore tokenStore, ITwitchWebSocketService twitchWebSocketService) =>
{
    var tokens = tokenStore.GetStatus();
    var botSocket = twitchWebSocketService.GetStatus(TwitchUserRole.Bot);
    var broadcasterSocket = twitchWebSocketService.GetStatus(TwitchUserRole.Broadcaster);

    return Results.Ok(new AuthStatusResponse(
        ToRoleStatus(tokens, TwitchUserRole.Bot),
        ToRoleStatus(tokens, TwitchUserRole.Broadcaster),
        ToConnectionStatus(botSocket),
        ToConnectionStatus(broadcasterSocket)));

    static RoleStatus? ToRoleStatus(IReadOnlyDictionary<TwitchUserRole, TwitchTokenStatus> tokens, TwitchUserRole role)
        => tokens.TryGetValue(role, out var status)
            ? new RoleStatus(status.Login, status.NeedsLogin, status.Scopes)
            : null;

    // Bot and Broadcaster are two independent WebSocket sessions and can fail independently
    // (see #98/#99), so their connection status is reported per-role rather than aggregated.
    static ConnectionStatus ToConnectionStatus(TwitchWebSocketStatus status)
        => new(status.Connected, status.LastMessageAtUtc == DateTime.MinValue ? null : status.LastMessageAtUtc);
});

app.MapGet("stream/status", (ITwitchWebSocketService twitchWebSocketService) =>
{
    var status = twitchWebSocketService.GetStreamStatus();
    return Results.Ok(new StreamStatusResponse(status.IsLive, status.StartedAt));
});

app.MapGet("hype-train/status", (ITwitchWebSocketService twitchWebSocketService) =>
{
    var status = twitchWebSocketService.GetHypeTrainStatus();
    return Results.Ok(status is null
        ? new HypeTrainStatusResponse(false, 0, 0, 0)
        : new HypeTrainStatusResponse(true, status.Level, status.Progress, status.Goal));
});

app.MapGet("goal/status", (ITwitchWebSocketService twitchWebSocketService) =>
{
    var status = twitchWebSocketService.GetGoalStatus();
    return Results.Ok(status is null
        ? new GoalStatusResponse(false, string.Empty, string.Empty, 0, 0)
        : new GoalStatusResponse(true, status.Type, status.Description, status.CurrentAmount, status.TargetAmount));
});

app.MapGet("poll/status", (ITwitchWebSocketService twitchWebSocketService) =>
{
    var status = twitchWebSocketService.GetPollStatus();
    return Results.Ok(status is null
        ? new PollStatusResponse(false, string.Empty, [])
        : new PollStatusResponse(true, status.Title, status.Choices.Select(c => new PollChoiceResponse(c.Title, c.Votes)).ToArray()));
});

app.MapGet("prediction/status", (ITwitchWebSocketService twitchWebSocketService) =>
{
    var status = twitchWebSocketService.GetPredictionStatus();
    return Results.Ok(status is null
        ? new PredictionStatusResponse(false, string.Empty, false, [])
        : new PredictionStatusResponse(true, status.Title, status.Locked,
            status.Outcomes.Select(o => new PredictionOutcomeResponse(o.Title, o.Color, o.Users, o.ChannelPoints)).ToArray()));
});

app.MapGet("ad-break/status", (ITwitchWebSocketService twitchWebSocketService) =>
{
    var status = twitchWebSocketService.GetAdBreakStatus();
    return Results.Ok(status is null
        ? new AdBreakStatusResponse(false, null, 0, false, string.Empty)
        : new AdBreakStatusResponse(true, status.StartedAt, status.DurationSeconds, status.IsAutomatic, status.RequesterUserName));
});

app.MapGet("activity/recent", (ITwitchActivityFeedService activityFeedService) =>
{
    var items = activityFeedService.GetRecent()
        .Select(e => new ActivityFeedItem(e.Kind.ToString(), e.OccurredAt, e.DisplayName, e.Summary))
        .ToArray();
    return Results.Ok(items);
});

app.MapGet("clips/recent", async (ITwitchClipService clipService, CancellationToken ct) =>
{
    var result = await clipService.GetRecentClipsAsync(cancellationToken: ct);
    return result.Success
        ? Results.Ok(result.Clips.Select(ToClipItem).ToArray())
        : Results.Problem(result.Error ?? "Failed to list clips", statusCode: StatusCodes.Status502BadGateway);
});

app.MapGet("moderation/recent", (ITwitchModerationLogService moderationLogService) =>
{
    var items = moderationLogService.GetRecent()
        .Select(e => new ModerationLogItem(e.Kind.ToString(), e.OccurredAt, e.ModeratorName, e.TargetName, e.Summary))
        .ToArray();
    return Results.Ok(items);
});

app.MapPost("chat/messages", async (
    SendChatMessageRequest request,
    ITwitchChatService chatService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest("Message must not be empty");
    }

    var result = await chatService.SendMessageAsync(request.Message, request.ReplyParentMessageId, cancellationToken);
    if (result.Sent)
    {
        return Results.Ok(new SendChatMessageResponse(result.Sent, result.MessageId, result.DropReason));
    }

    if (result.RateLimited)
    {
        return Results.Problem(
            result.DropReason ?? "Rate limited by Twitch",
            statusCode: StatusCodes.Status429TooManyRequests,
            extensions: new Dictionary<string, object?> { ["retryAfter"] = result.RateLimitResetAt });
    }

    return Results.Problem(result.DropReason ?? "Message was not sent", statusCode: StatusCodes.Status502BadGateway);
});

app.MapPost("chat/shoutouts", async (
    SendShoutoutRequest request,
    ITwitchShoutoutService shoutoutService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ToBroadcasterLogin))
    {
        return Results.BadRequest("ToBroadcasterLogin must not be empty");
    }

    var result = await shoutoutService.SendShoutoutAsync(request.ToBroadcasterLogin, cancellationToken);
    if (result.Success)
    {
        return Results.Ok(new SendShoutoutResponse(true));
    }

    if (result.RateLimited)
    {
        return Results.Problem(result.Error ?? "Rate limited by Twitch", statusCode: StatusCodes.Status429TooManyRequests);
    }

    return Results.Problem(result.Error ?? "Failed to send shoutout", statusCode: StatusCodes.Status502BadGateway);
});

app.MapPost("chat/announcements", async (
    SendAnnouncementRequest request,
    ITwitchAnnouncementService announcementService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest("Message must not be empty");
    }

    var result = await announcementService.SendAnnouncementAsync(request.Message, request.Color, cancellationToken);
    if (result.Success)
    {
        return Results.Ok(new SendAnnouncementResponse(true));
    }

    if (result.RateLimited)
    {
        return Results.Problem(result.Error ?? "Rate limited by Twitch", statusCode: StatusCodes.Status429TooManyRequests);
    }

    return Results.Problem(result.Error ?? "Failed to send announcement", statusCode: StatusCodes.Status502BadGateway);
});

app.MapGet("ad-schedule", async (
    ITwitchAdScheduleService adScheduleService,
    CancellationToken cancellationToken) =>
{
    var result = await adScheduleService.GetAdScheduleAsync(cancellationToken);
    return result.Success
        ? Results.Ok(ToAdScheduleResponse(result.Schedule!))
        : Results.Problem(result.Error ?? "Failed to get ad schedule", statusCode: StatusCodes.Status502BadGateway);
});

app.MapPost("ad-schedule/snooze", async (
    ITwitchAdScheduleService adScheduleService,
    CancellationToken cancellationToken) =>
{
    var result = await adScheduleService.SnoozeNextAdAsync(cancellationToken);
    return result.Success
        ? Results.Ok(new AdSnoozeResponse(result.Snooze!.SnoozeCount, result.Snooze.SnoozeRefreshAt, result.Snooze.NextAdAt))
        : Results.Problem(result.Error ?? "Failed to snooze next ad", statusCode: StatusCodes.Status502BadGateway);
});

app.MapGet("rewards", async (
    ITwitchRewardService rewardService,
    CancellationToken cancellationToken) =>
{
    var result = await rewardService.GetRewardsAsync(cancellationToken);
    return result.Success
        ? Results.Ok(result.Rewards.Select(ToRewardResponse).ToArray())
        : Results.Problem(result.Error ?? "Failed to list rewards", statusCode: StatusCodes.Status502BadGateway);
});

app.MapPost("rewards", async (
    CreateRewardHttpRequest request,
    ITwitchRewardService rewardService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest("Title must not be empty");
    }

    if (request.Cost <= 0)
    {
        return Results.BadRequest("Cost must be greater than zero");
    }

    var result = await rewardService.CreateRewardAsync(
        new CreateRewardRequest(
            request.Title,
            request.Cost,
            request.Prompt,
            request.IsEnabled,
            request.BackgroundColor,
            request.IsUserInputRequired,
            request.ShouldRedemptionsSkipRequestQueue,
            request.GlobalCooldownSeconds,
            request.MaxPerStream,
            request.MaxPerUserPerStream),
        cancellationToken);

    return result.Success
        ? Results.Ok(ToRewardResponse(result.Reward!))
        : Results.Problem(result.Error ?? "Failed to create reward", statusCode: StatusCodes.Status502BadGateway);
});

app.MapPatch("rewards/{rewardId}", async (
    string rewardId,
    UpdateRewardHttpRequest request,
    ITwitchRewardService rewardService,
    CancellationToken cancellationToken) =>
{
    var result = await rewardService.UpdateRewardAsync(
        rewardId,
        new UpdateRewardRequest(
            request.Title,
            request.Cost,
            request.Prompt,
            request.IsEnabled,
            request.BackgroundColor,
            request.IsUserInputRequired,
            request.IsPaused,
            request.ShouldRedemptionsSkipRequestQueue,
            request.GlobalCooldownSeconds,
            request.MaxPerStream,
            request.MaxPerUserPerStream),
        cancellationToken);

    return result.Success
        ? Results.Ok(ToRewardResponse(result.Reward!))
        : Results.Problem(result.Error ?? "Failed to update reward", statusCode: StatusCodes.Status502BadGateway);
});

app.MapPatch("rewards/{rewardId}/pause", async (
    string rewardId,
    SetRewardPausedRequest request,
    ITwitchRewardService rewardService,
    CancellationToken cancellationToken) =>
{
    var result = await rewardService.SetPausedAsync(rewardId, request.IsPaused, cancellationToken);

    return result.Success
        ? Results.Ok(ToRewardResponse(result.Reward!))
        : Results.Problem(result.Error ?? "Failed to update reward status", statusCode: StatusCodes.Status502BadGateway);
});

app.MapDelete("rewards/{rewardId}", async (
    string rewardId,
    ITwitchRewardService rewardService,
    CancellationToken cancellationToken) =>
{
    var result = await rewardService.DeleteRewardAsync(rewardId, cancellationToken);

    return result.Success
        ? Results.NoContent()
        : Results.Problem(result.Error ?? "Failed to delete reward", statusCode: StatusCodes.Status502BadGateway);
});

app.Run();

static AdScheduleResponse ToAdScheduleResponse(AdSchedule schedule) =>
    new(schedule.NextAdAt, schedule.LastAdAt, schedule.DurationSeconds, schedule.PrerollFreeTimeSeconds, schedule.SnoozeCount, schedule.SnoozeRefreshAt);

static RewardResponse ToRewardResponse(CustomReward reward) =>
    new(reward.Id, reward.Title, reward.Cost, reward.Prompt, reward.IsEnabled, reward.IsPaused, reward.BackgroundColor,
        reward.IsUserInputRequired, reward.ShouldRedemptionsSkipRequestQueue,
        reward.GlobalCooldownSeconds, reward.MaxPerStream, reward.MaxPerUserPerStream);

static ClipItem ToClipItem(Clip clip) =>
    new(clip.Id, clip.Url, clip.Title, clip.CreatorName, clip.ViewCount, clip.CreatedAt, clip.ThumbnailUrl, clip.DurationSeconds);

public record AuthCallbackRequest(string Code, string Scopes, string RedirectUri);

public record SendChatMessageRequest(string Message, string? ReplyParentMessageId = null);

public record SendChatMessageResponse(bool Sent, string? MessageId, string? DropReason);

public record SendShoutoutRequest(string ToBroadcasterLogin);

public record SendShoutoutResponse(bool Sent);

public record SendAnnouncementRequest(string Message, string? Color = null);

public record SendAnnouncementResponse(bool Sent);

public record AuthCallbackResponse(string Role, string Login);

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

public record AdBreakStatusResponse(bool IsActive, DateTimeOffset? StartedAt, int DurationSeconds, bool IsAutomatic, string RequesterUserName);

public record ClipItem(string Id, string Url, string Title, string CreatorName, int ViewCount, DateTimeOffset CreatedAt, string ThumbnailUrl, int DurationSeconds);

public record AdScheduleResponse(DateTimeOffset? NextAdAt, DateTimeOffset? LastAdAt, int DurationSeconds, int PrerollFreeTimeSeconds, int SnoozeCount, DateTimeOffset? SnoozeRefreshAt);

public record AdSnoozeResponse(int SnoozeCount, DateTimeOffset? SnoozeRefreshAt, DateTimeOffset? NextAdAt);

public record ActivityFeedItem(string Kind, DateTimeOffset OccurredAt, string DisplayName, string Summary);

public record ModerationLogItem(string Kind, DateTimeOffset OccurredAt, string ModeratorName, string TargetName, string Summary);

public record CreateRewardHttpRequest(
    string Title,
    int Cost,
    string? Prompt = null,
    bool IsEnabled = true,
    string? BackgroundColor = null,
    bool IsUserInputRequired = false,
    bool ShouldRedemptionsSkipRequestQueue = false,
    int? GlobalCooldownSeconds = null,
    int? MaxPerStream = null,
    int? MaxPerUserPerStream = null);

public record UpdateRewardHttpRequest(
    string? Title = null,
    int? Cost = null,
    string? Prompt = null,
    bool? IsEnabled = null,
    string? BackgroundColor = null,
    bool? IsUserInputRequired = null,
    bool? IsPaused = null,
    bool? ShouldRedemptionsSkipRequestQueue = null,
    int? GlobalCooldownSeconds = null,
    int? MaxPerStream = null,
    int? MaxPerUserPerStream = null);

public record SetRewardPausedRequest(bool IsPaused);

public record RewardResponse(
    string Id,
    string Title,
    int Cost,
    string Prompt,
    bool IsEnabled,
    bool IsPaused,
    string BackgroundColor,
    bool IsUserInputRequired,
    bool ShouldRedemptionsSkipRequestQueue,
    int? GlobalCooldownSeconds,
    int? MaxPerStream,
    int? MaxPerUserPerStream);
