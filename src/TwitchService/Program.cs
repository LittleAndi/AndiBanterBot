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

app.Run();

public record AuthCallbackRequest(string Code, string Scopes, string RedirectUri);

public record AuthCallbackResponse(string Role, string Login);
