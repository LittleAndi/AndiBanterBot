namespace OverlayService.Overlays.Starting;

// "Stream starting soon" screen. Static/client-side only (see #134) — the
// countdown target arrives as a query-string param (?startsAt=...), so there's
// no data endpoint to register alongside the module itself.
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
            AssetBundlePath: OverlayModule.DefaultAssetBundlePath("starting")));
    }
}
