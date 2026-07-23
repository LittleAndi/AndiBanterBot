namespace Web.Components.Services;

public class OverlayApiClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private readonly HttpClient httpClient = httpClientFactory.CreateClient("overlays");
    private readonly string publicBaseUrl = configuration["Overlays:PublicBaseUrl"]?.TrimEnd('/') ?? string.Empty;

    // Same fail-fast reasoning as TwitchApiClient: read as "unreachable" within a
    // couple of seconds instead of waiting out the resilience handler's retry budget.
    public async Task<OverlayModuleItem[]?> GetOverlays()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            return await httpClient.GetFromJsonAsync<OverlayModuleItem[]>("overlays", cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }

    // The browser-source URL the streamer pastes into OBS. Overlays are served by
    // OverlayService, not Web, so the public host comes from Overlays:PublicBaseUrl
    // config; when that's unset we hand back the relative path for the operator to
    // complete against wherever OverlayService is reachable.
    public string BuildBrowserSourceUrl(string slug) =>
        string.IsNullOrEmpty(publicBaseUrl) ? $"/overlay/{slug}" : $"{publicBaseUrl}/overlay/{slug}";
}

public record OverlayModuleItem(string Slug, string DisplayName, string Description, int DefaultWidth, int DefaultHeight);
