namespace OverlayService.Overlays;

// "Clip created" toast overlay: notifies when TwitchClipTriggerService auto-creates a clip
// on a hype-train finish or a big cheer. Polls TwitchService's lightweight in-memory buffer
// (clips/auto-created/recent) rather than clips/recent, which hits the live Twitch Helix API
// on every call and would hammer rate limits under always-on overlay polling.
public static class HighlightsOverlayModule
{
    public static void MapHighlightsOverlay(this WebApplication app)
    {
        app.Services.GetRequiredService<IOverlayRegistry>().Register(new OverlayModule(
            Slug: "highlights",
            DisplayName: "Highlights",
            Description: "Toast notification when a clip is auto-created from a hype train finish or a big cheer.",
            DefaultWidth: 500,
            DefaultHeight: 200,
            AssetBundlePath: OverlayModule.DefaultAssetBundlePath("highlights")));

        app.MapGet("/overlay/highlights/events", async (IHttpClientFactory httpClientFactory, CancellationToken ct) =>
        {
            var client = httpClientFactory.CreateClient("twitch");
            var response = await client.GetAsync("clips/auto-created/recent", ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            return response.IsSuccessStatusCode
                ? Results.Content(json, "application/json")
                : Results.Problem("Failed to fetch auto-created clips", statusCode: (int)response.StatusCode);
        });
    }
}
