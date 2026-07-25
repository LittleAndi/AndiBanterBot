namespace OverlayService.Overlays;

// Describes one query-string knob a module's client-side app.js reads via
// overlayConfig() (see wwwroot/overlays/_shared/overlay-client.js) - nothing
// persists server-side, so this is purely metadata for Web's settings UI to
// render an input and know what to append to the browser-source URL.
// IsPrimary marks per-stream content worth showing inline every time (a countdown
// target, a sign-off message) as opposed to set-once visual/behavioral tuning
// (poll intervals, animation speed) that Web tucks behind a closed-by-default
// disclosure so the card stays glanceable.
public record OverlaySettingDefinition(
    string Key,
    string Label,
    OverlaySettingType Type,
    string DefaultValue,
    IReadOnlyList<string>? Options = null,
    string? HelperText = null,
    bool IsPrimary = false);

public enum OverlaySettingType
{
    Text,
    Number,
    Select,
    DateTime,
}
