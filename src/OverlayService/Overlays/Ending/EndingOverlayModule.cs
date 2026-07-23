namespace OverlayService.Overlays;

public static class EndingOverlayModule
{
    public static WebApplication MapEndingOverlay(this WebApplication app)
    {
        var registry = app.Services.GetRequiredService<IOverlayRegistry>();

        registry.Register(new OverlayModule(
            Slug: "ending",
            DisplayName: "Stream Ending",
            Description: "Outro/BRB screen with a customizable sign-off message for when the stream wraps up.",
            DefaultWidth: 1920,
            DefaultHeight: 1080,
            AssetBundlePath: OverlayModule.DefaultAssetBundlePath("ending")));

        return app;
    }
}
