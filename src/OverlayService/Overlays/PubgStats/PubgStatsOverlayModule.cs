using System.Net.Http.Json;

namespace OverlayService.Overlays;

public static class PubgStatsOverlayModule
{
    private const string Slug = "pubg-stats";

    public static void MapPubgStatsOverlay(this WebApplication app)
    {
        app.Services.GetRequiredService<IOverlayRegistry>().Register(new OverlayModule(
            Slug,
            "PUBG Match Stats",
            "Compact kills/placement/damage readout for the streamer's most recent PUBG match.",
            360,
            160,
            OverlayModule.DefaultAssetBundlePath(Slug)));

        // Proxies PubgService's `GET stats/recent` (most-recent-first) and trims it to
        // just the newest match — the overlay only ever shows the latest game's stats.
        app.MapGet("/overlay/pubg-stats/events", async (IHttpClientFactory httpClientFactory) =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var client = httpClientFactory.CreateClient("pubg");
                var items = await client.GetFromJsonAsync<PubgMatchStatsResponse[]>("stats/recent", cts.Token);
                return Results.Ok(items?.FirstOrDefault());
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                return Results.Ok((PubgMatchStatsResponse?)null);
            }
        });
    }
}

// Mirrors PubgService's MatchStatsItem/PlayerStatsItem response shape (GET stats/recent).
public record PubgMatchStatsResponse(
    string MatchId,
    DateTimeOffset CreatedAt,
    string MapName,
    string GameMode,
    string MatchType,
    long DurationSeconds,
    bool IsCustomMatch,
    PubgPlayerStatsResponse? PlayerStats);

public record PubgPlayerStatsResponse(
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
