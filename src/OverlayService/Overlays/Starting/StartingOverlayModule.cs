namespace OverlayService.Overlays.Starting;

// "Stream starting soon" screen. The title still rides the query string like
// every other overlay setting (see overlayConfig() in overlay-client.js), but
// the countdown target does not (see #151 follow-up) — OBS Browser sources
// store whatever URL you paste in, so baking a fresh startsAt into it meant
// re-pasting the URL before every stream. Instead the target is persisted
// server-side via IStartingCountdownStore and served from /overlay/starting/events,
// which app.js polls (see pollEvents() in overlay-client.js) — the reserved
// per-module data-endpoint convention mentioned in Program.cs, now in use.
public static class StartingOverlayModule
{
    public static void MapStartingOverlay(this WebApplication app)
    {
        var registry = app.Services.GetRequiredService<IOverlayRegistry>();

        registry.Register(new OverlayModule(
            Slug: "starting",
            DisplayName: "Starting Soon",
            Description: "Countdown screen shown before the stream goes live.",
            DefaultWidth: 1920,
            DefaultHeight: 1080,
            AssetBundlePath: OverlayModule.DefaultAssetBundlePath("starting"),
            Settings:
            [
                new OverlaySettingDefinition("title", "Title", OverlaySettingType.Text, "Stream starting soon!", IsPrimary: true),
            ]));

        var countdown = app.Services.GetRequiredService<IStartingCountdownStore>();

        app.MapGet("/overlay/starting/events", () => Results.Ok(new StartingCountdownResponse(countdown.Get())));

        app.MapPost("/overlay/starting/events", (StartingCountdownRequest request) =>
        {
            countdown.Set(request.StartsAt);
            return Results.Ok(new StartingCountdownResponse(countdown.Get()));
        });
    }
}

public record StartingCountdownRequest(DateTimeOffset? StartsAt);
public record StartingCountdownResponse(DateTimeOffset? StartsAt);
