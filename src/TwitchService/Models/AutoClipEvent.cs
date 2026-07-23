namespace Application.Features.Twitch;

public record AutoClipEvent(
    string ClipId,
    DateTimeOffset OccurredAt,
    string HighlightDescription);
