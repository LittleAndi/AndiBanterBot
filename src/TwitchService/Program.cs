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

    // Connects and returns; no-op if the websocket is already connecting or connected
    try
    {
        await twitchWebSocketService.Start();
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
    var socket = twitchWebSocketService.GetStatus();

    return Results.Ok(new AuthStatusResponse(
        ToRoleStatus(tokens, TwitchUserRole.Bot),
        ToRoleStatus(tokens, TwitchUserRole.Broadcaster),
        socket.Connected,
        socket.LastMessageAtUtc == DateTime.MinValue ? null : socket.LastMessageAtUtc));

    static RoleStatus? ToRoleStatus(IReadOnlyDictionary<TwitchUserRole, TwitchTokenStatus> tokens, TwitchUserRole role)
        => tokens.TryGetValue(role, out var status)
            ? new RoleStatus(status.Login, status.NeedsLogin, status.Scopes)
            : null;
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
    return result.Sent
        ? Results.Ok(new SendChatMessageResponse(result.Sent, result.MessageId, result.DropReason))
        : Results.Problem(result.DropReason ?? "Message was not sent", statusCode: StatusCodes.Status502BadGateway);
});

app.Run();

public record AuthCallbackRequest(string Code, string Scopes, string RedirectUri);

public record SendChatMessageRequest(string Message, string? ReplyParentMessageId = null);

public record SendChatMessageResponse(bool Sent, string? MessageId, string? DropReason);

public record AuthCallbackResponse(string Role, string Login);

public record RoleStatus(string Login, bool NeedsLogin, string[] Scopes);

public record AuthStatusResponse(RoleStatus? Bot, RoleStatus? Broadcaster, bool WebSocketConnected, DateTime? LastMessageAtUtc);
