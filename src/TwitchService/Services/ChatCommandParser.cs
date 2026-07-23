namespace Application.Features.Twitch;

public record ChatCommand(string Name, IReadOnlyList<string> Args);

/// <summary>
/// Pure recognition of chat commands out of raw message text - a shared grammar so chat
/// games and suggestions (later modules, built in OverlayService) don't each parse "!vote 2"
/// their own way. No dispatch/handling lives here, only recognition - see this issue's
/// non-goals.
/// </summary>
public static class ChatCommandParser
{
    private const char Prefix = '!';

    public static ChatCommand? TryParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text[0] != Prefix)
        {
            return null;
        }

        var parts = text[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        return new ChatCommand(parts[0].ToLowerInvariant(), parts[1..]);
    }
}
