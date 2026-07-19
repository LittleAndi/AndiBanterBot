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

app.MapGet("activity/recent", (ITwitchActivityFeedService activityFeedService) =>
{
    var items = activityFeedService.GetRecent()
        .Select(e => new ActivityFeedItem(e.Kind.ToString(), e.OccurredAt, e.DisplayName, e.Summary))
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

app.Run();

public record AuthCallbackRequest(string Code, string Scopes, string RedirectUri);

public record SendChatMessageRequest(string Message, string? ReplyParentMessageId = null);

public record SendChatMessageResponse(bool Sent, string? MessageId, string? DropReason);

public record AuthCallbackResponse(string Role, string Login);

public record RoleStatus(string Login, bool NeedsLogin, string[] Scopes);

public record ConnectionStatus(bool Connected, DateTime? LastMessageAtUtc);

public record AuthStatusResponse(RoleStatus? Bot, RoleStatus? Broadcaster, ConnectionStatus BotConnection, ConnectionStatus BroadcasterConnection);

public record StreamStatusResponse(bool? IsLive, DateTimeOffset? StartedAt);

public record HypeTrainStatusResponse(bool IsActive, int Level, int Progress, int Goal);

public record GoalStatusResponse(bool IsActive, string Type, string Description, int CurrentAmount, int TargetAmount);

public record ActivityFeedItem(string Kind, DateTimeOffset OccurredAt, string DisplayName, string Summary);
