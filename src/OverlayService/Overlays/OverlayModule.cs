namespace OverlayService.Overlays;

// The shared contract every overlay module implements. A module is a browser
// source the streamer drops into OBS: its page is served at /overlay/{Slug}
// from the static bundle under wwwroot/{AssetBundlePath}, and per-instance
// tweaks arrive as query-string params on that URL (e.g. ?theme=dark) rather
// than any persisted config. DefaultWidth/DefaultHeight are the suggested OBS
// browser-source dimensions.
public record OverlayModule(
    string Slug,
    string DisplayName,
    string Description,
    int DefaultWidth,
    int DefaultHeight,
    string AssetBundlePath)
{
    // Conventional bundle location for a module that doesn't override it:
    // wwwroot/overlays/{slug}/, served as static files at /overlays/{slug}/.
    public static string DefaultAssetBundlePath(string slug) => $"overlays/{slug}";
}
