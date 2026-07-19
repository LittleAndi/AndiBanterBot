using System.Web;

namespace Web.Components.Services;

/// <summary>
/// Builds the Twitch OAuth authorize URLs for the bot and broadcaster accounts.
/// Shared by any page that offers a (re-)authorize link, so the scope/redirect
/// logic lives in one place.
/// </summary>
public class TwitchAuthUrlBuilder(IConfiguration configuration)
{
    // Hardcoded rather than config-driven for now. The plan is for the scopes each
    // account needs to eventually be derived from whichever features/modules are
    // enabled (possibly user-selectable), not a static config list.
    private static readonly string[] BotScopes =
    [
        "user:bot",
        "user:read:chat",
        "chat:read",
        "chat:edit",
        "whispers:read",
    ];

    private static readonly string[] BroadcasterScopes =
    [
        "channel:bot",
        "bits:read",
        "channel:manage:predictions",
        "channel:manage:redemptions",
        "channel:read:ads",
        "channel:read:redemptions",
        "channel:read:subscriptions",
        "channel:read:vips",
        "clips:edit",
        "moderator:read:followers",
    ];

    public string BuildBotAuthorizeUrl(string baseUri) =>
        BuildUrl(baseUri, "callback", BotScopes);

    public string BuildBroadcasterAuthorizeUrl(string baseUri) =>
        BuildUrl(baseUri, "pubsubcallback", BroadcasterScopes);

    private string BuildUrl(string baseUri, string redirectPath, string[] scopes)
    {
        var clientId = configuration["TwitchLib:ClientId"]!;
        var encodedScopes = HttpUtility.UrlEncode(string.Join(" ", scopes));
        var redirectUri = HttpUtility.UrlEncode(baseUri + redirectPath);

        return $"https://id.twitch.tv/oauth2/authorize?response_type=code&client_id={clientId}&redirect_uri={redirectUri}&scope={encodedScopes}";
    }
}
