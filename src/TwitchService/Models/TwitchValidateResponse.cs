namespace Application.Features.Twitch;

public class TwitchValidateResponse
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; init; } = string.Empty;

    [JsonPropertyName("login")]
    public string Login { get; init; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = string.Empty;

    [JsonPropertyName("scopes")]
    public string[] Scopes { get; init; } = [];

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
}
