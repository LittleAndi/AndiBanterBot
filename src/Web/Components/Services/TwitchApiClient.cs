namespace Web.Components.Services;

public class TwitchApiClient(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient httpClient = httpClientFactory.CreateClient("twitch");

    public async Task StartEventSub(string code, string scopes)
    {
        await httpClient.PostAsJsonAsync("start-eventsub", new EventSubStartRequest(code, scopes));
    }

    public async Task SubscribeToBroadcasterSubscriptions(string code, string scopes)
    {
        await httpClient.PostAsJsonAsync("broadcaster-subscriptions", new BroadcasterSubscriptionsRequest(code, scopes));
    }
}

public record EventSubStartRequest(string Code, string Scopes);
public record BroadcasterSubscriptionsRequest(string Code, string Scopes);