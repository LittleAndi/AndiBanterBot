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

app.MapPost("start-eventsub", async (EventSubStartRequest startRequest, ITwitchWebSocketService twitchWebSocketService) =>
{
    await twitchWebSocketService.Start();
});

app.MapPost("broadcaster-subscriptions", async (BroadcasterSubscriptionsRequest request, ITwitchWebSocketService twitchWebSocketService) =>
{
    await twitchWebSocketService.Start();
    await twitchWebSocketService.SubscribeToBroadcasterSubscriptions(request);
});

app.Run();

public record EventSubStartRequest(string Code, string Scopes);

public record BroadcasterSubscriptionsRequest(string Code, string Scopes);