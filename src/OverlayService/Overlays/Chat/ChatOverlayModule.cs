namespace OverlayService.Overlays;

// Chat overlay: renders live Twitch chat as an OBS browser source. TwitchService has no
// external HTTP endpoint (no .WithExternalHttpEndpoints() in Host), so the browser can't
// call it directly - this proxies chat/recent verbatim rather than reshaping it, since the
// browser JS already consumes ChatFeedItem's shape as-is.
public static class ChatOverlayModule
{
    public static void MapChatOverlay(this WebApplication app)
    {
        app.Services.GetRequiredService<IOverlayRegistry>().Register(new OverlayModule(
            Slug: "chat",
            DisplayName: "Chat",
            Description: "Live Twitch chat feed for an OBS browser source.",
            DefaultWidth: 400,
            DefaultHeight: 600,
            AssetBundlePath: OverlayModule.DefaultAssetBundlePath("chat")));

        app.MapGet("/overlay/chat/events", async (IHttpClientFactory httpClientFactory, CancellationToken ct) =>
        {
            var client = httpClientFactory.CreateClient("twitch");
            var response = await client.GetAsync("chat/recent", ct);
            if (!response.IsSuccessStatusCode)
            {
                return Results.Problem("Failed to fetch chat feed from TwitchService", statusCode: (int)response.StatusCode);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return Results.Content(json, "application/json");
        });
    }
}
