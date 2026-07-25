using System.Net.Http.Json;

namespace OverlayService.Overlays;

// "Emote rain" overlay: an OBS browser source that animates recently used chat
// emotes. Reuses TwitchService's chat/recent feed (same one the chat-text overlay
// consumes) but strips everything down to just the emote fragments - the client
// doesn't need message text, chatter identity, etc. TwitchService is internal-only
// so this proxy endpoint is required, not optional.
public static class EmotesOverlayModule
{
    private const string EmoteCdnUrlTemplate = "https://static-cdn.jtvnw.net/emoticons/v2/{0}/default/dark/3.0";

    public static void MapEmotesOverlay(this WebApplication app)
    {
        app.Services.GetRequiredService<IOverlayRegistry>().Register(new OverlayModule(
            Slug: "emotes",
            DisplayName: "Emote Rain",
            Description: "Animates recently used chat emotes drifting across the scene.",
            DefaultWidth: 1920,
            DefaultHeight: 1080,
            AssetBundlePath: OverlayModule.DefaultAssetBundlePath("emotes"),
            RequiresBotAuth: true,
            Settings:
            [
                new OverlaySettingDefinition("direction", "Direction", OverlaySettingType.Select, "down", ["down", "up", "left", "right"]),
                new OverlaySettingDefinition("speed", "Speed (seconds to cross)", OverlaySettingType.Number, "6"),
                new OverlaySettingDefinition("size", "Emote size (px)", OverlaySettingType.Number, "64"),
                new OverlaySettingDefinition("pollMs", "Poll interval (ms)", OverlaySettingType.Number, "2500"),
            ]));

        app.MapGet("/overlay/emotes/events", async (IHttpClientFactory httpClientFactory, CancellationToken ct) =>
        {
            var client = httpClientFactory.CreateClient("twitch");
            var messages = await client.GetFromJsonAsync<ChatFeedMessageDto[]>("chat/recent", ct) ?? [];

            var occurrences = messages
                .SelectMany(m => m.Fragments
                    .Select((fragment, index) => (fragment, index))
                    .Where(x => x.fragment.Emote is not null)
                    .Select(x => new EmoteOccurrence(
                        m.MessageId,
                        x.index,
                        x.fragment.Emote!.Id,
                        string.Format(EmoteCdnUrlTemplate, x.fragment.Emote.Id),
                        m.OccurredAt)))
                .ToArray();

            return Results.Ok(occurrences);
        });
    }
}

// Subset of TwitchService's ChatFeedItem/ChatFeedFragmentItem/ChatFeedEmoteItem
// shape - only the fields this module needs to deserialize chat/recent.
public record ChatFeedMessageDto(string MessageId, DateTimeOffset OccurredAt, IReadOnlyList<ChatFeedFragmentDto> Fragments);

public record ChatFeedFragmentDto(string Type, ChatFeedEmoteDto? Emote);

public record ChatFeedEmoteDto(string Id);

public record EmoteOccurrence(string MessageId, int FragmentIndex, string EmoteId, string Url, DateTimeOffset OccurredAt);
