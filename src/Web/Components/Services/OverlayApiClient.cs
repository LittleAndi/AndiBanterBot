namespace Web.Components.Services;

public class OverlayApiClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private readonly HttpClient httpClient = httpClientFactory.CreateClient("overlays");
    private readonly string publicBaseUrl = configuration["Overlays:PublicBaseUrl"]?.TrimEnd('/') ?? string.Empty;

    // Aspire injects the "overlays" resource's actual resolved endpoint addresses as
    // services:overlays:<endpoint>:<index> config (from the services__overlays__* env
    // vars the AppHost sets, re-resolved fresh on every run) - this is the real,
    // browser-reachable address, unlike "https+http://overlays", which is only a
    // logical placeholder meaningful to the service-discovery HttpClient handler
    // and shows up unresolved if surfaced directly (see git history for why this
    // isn't read off the GetOverlays() response instead).
    private readonly string? resolvedBaseUrl =
        configuration["services:overlays:https:0"]?.TrimEnd('/')
        ?? configuration["services:overlays:http:0"]?.TrimEnd('/');

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
    // config when set (production override); otherwise falls back to the address
    // resolved by GetOverlays(), and only to a bare relative path if that never ran.
    // queryParams carries whatever settings the operator has changed from their
    // defaults - see Overlays.razor - so the copied URL bakes those in.
    public string BuildBrowserSourceUrl(string slug, IReadOnlyDictionary<string, string>? queryParams = null)
    {
        var baseUrl = !string.IsNullOrEmpty(publicBaseUrl) ? publicBaseUrl : resolvedBaseUrl;
        var path = baseUrl is null ? $"/overlay/{slug}" : $"{baseUrl}/overlay/{slug}";

        if (queryParams is null || queryParams.Count == 0)
        {
            return path;
        }

        var query = string.Join("&", queryParams.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"{path}?{query}";
    }

    // The starting overlay's countdown target lives server-side (OverlayService's
    // IStartingCountdownStore), not on the browser-source URL - see StartingOverlayModule
    // for why. Reads/writes hit its /overlay/starting/events endpoint directly.
    public async Task<DateTimeOffset?> GetStartingCountdown()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await httpClient.GetFromJsonAsync<StartingCountdownItem>("overlay/starting/events", cts.Token);
            return response?.StartsAt;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<bool> SetStartingCountdown(DateTimeOffset? startsAt)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await httpClient.PostAsJsonAsync("overlay/starting/events", new StartingCountdownItem(startsAt), cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return false;
        }
    }
}

public record StartingCountdownItem(DateTimeOffset? StartsAt);

public record OverlayModuleItem(
    string Slug,
    string DisplayName,
    string Description,
    int DefaultWidth,
    int DefaultHeight,
    bool RequiresBotAuth,
    bool RequiresBroadcasterAuth,
    IReadOnlyList<OverlaySettingItem> Settings);

// Mirrors OverlayService's OverlaySettingResponse/OverlaySettingDefinition - the two
// projects only share a JSON contract, not a project reference (see the OverlayModule
// duplication this already follows).
public record OverlaySettingItem(
    string Key,
    string Label,
    OverlaySettingType Type,
    string DefaultValue,
    IReadOnlyList<string>? Options,
    string? HelperText,
    bool IsPrimary);

public enum OverlaySettingType
{
    Text,
    Number,
    Select,
}
