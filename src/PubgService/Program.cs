var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddPubg(builder.Configuration);
builder.Services.AddHostedService<PubgBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("stats/recent", (IPubgMatchFeedService matchFeedService) =>
{
    var items = matchFeedService.GetRecent()
        .Select(ToMatchStatsItem)
        .ToArray();
    return Results.Ok(items);
});

app.Run();

static MatchStatsItem ToMatchStatsItem(PubgMatchSummary summary) =>
    new(summary.MatchId, summary.CreatedAt, summary.MapName, summary.GameMode, summary.MatchType,
        summary.DurationSeconds, summary.IsCustomMatch, ToPlayerStatsItem(summary.PlayerStats));

static PlayerStatsItem? ToPlayerStatsItem(PubgPlayerStats? stats) =>
    stats is null
        ? null
        : new PlayerStatsItem(stats.Kills, stats.Assists, stats.DbnOs, stats.DamageDealt, stats.HeadshotKills,
            stats.Heals, stats.KillPlace, stats.WinPlace, stats.LongestKill, stats.Revives, stats.TimeSurvived, stats.Boosts);

public record MatchStatsItem(
    string MatchId,
    DateTimeOffset CreatedAt,
    string MapName,
    string GameMode,
    string MatchType,
    long DurationSeconds,
    bool IsCustomMatch,
    PlayerStatsItem? PlayerStats);

public record PlayerStatsItem(
    long? Kills,
    long? Assists,
    long? DbnOs,
    double? DamageDealt,
    long? HeadshotKills,
    long? Heals,
    long? KillPlace,
    long? WinPlace,
    double? LongestKill,
    long? Revives,
    long? TimeSurvived,
    long? Boosts);
