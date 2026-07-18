using System.Web;

namespace Web.Components.Services;

/// <summary>
/// Builds the Twitch OAuth authorize URLs for the bot and broadcaster accounts.
/// Shared by any page that offers a (re-)authorize link, so the scope/redirect
/// logic lives in one place.
/// </summary>
public class TwitchAuthUrlBuilder(IConfiguration configuration)
{
    public string BuildBotAuthorizeUrl(string baseUri) =>
        BuildUrl(baseUri, "callback", "TwitchLib:BotScopes");

    public string BuildBroadcasterAuthorizeUrl(string baseUri) =>
        BuildUrl(baseUri, "pubsubcallback", "TwitchLib:BroadcasterScopes");

    private string BuildUrl(string baseUri, string redirectPath, string scopesSection)
    {
        var clientId = configuration["TwitchLib:ClientId"]!;
        var scopes = HttpUtility.UrlEncode(string.Join(" ", configuration.GetSection(scopesSection).Get<string[]>()!));
        var redirectUri = HttpUtility.UrlEncode(baseUri + redirectPath);

        return $"https://id.twitch.tv/oauth2/authorize?response_type=code&client_id={clientId}&redirect_uri={redirectUri}&scope={scopes}";
    }
}
