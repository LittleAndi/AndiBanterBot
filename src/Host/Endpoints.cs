using System.Web;

namespace Host.Endpoints;

public static class Endpoints
{
    public static WebApplication MapEndpoints(this WebApplication app, IConfiguration configuration)
    {
        var clientId = configuration["TwitchLib:ClientId"];
        var clientSecret = configuration["TwitchLib:ClientSecret"];
        var scopes = Endpoints.BuildScopes(configuration!.GetSection("TwitchLib:Scopes")!.Get<string[]>()!);

        app.MapGet("/", () =>
        {
            // Return HTML with hello world
            return Results.Text($@"
                <body bgcolor='#111111'>
                    <a href='https://id.twitch.tv/oauth2/authorize?response_type=code&client_id={clientId}&redirect_uri=http%3A%2F%2Flocalhost%3A5000%2Fcallback&scope={scopes}'>Authorize Bot with bot account</a>
                    <a href='https://id.twitch.tv/oauth2/authorize?response_type=code&client_id={clientId}&redirect_uri=http%3A%2F%2Flocalhost%3A5000%2Fpubsubcallback&scope={scopes}'>Authorize Bot with stream account</a>
                </body>",
                "text/html"
            );
        });

        app.MapGet("/callback", async (
            HttpContext context,
            IChatService chatService,
            IClipService clipService,
            IMonitorService monitorService
        ) =>
        {
            var authorizationCode = context.Request.Query["code"];

            // Use the authorization code to get the access token from https://id.twitch.tv/oauth2/token with a http post
            HttpClient client = new()
            {
                BaseAddress = new Uri("https://id.twitch.tv/oauth2/")
            };
            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string?, string?>("client_id", clientId),
                new KeyValuePair<string?, string?>("client_secret", clientSecret),
                new KeyValuePair<string?, string?>("code", authorizationCode),
                new KeyValuePair<string?, string?>("grant_type", "authorization_code"),
                new KeyValuePair<string?, string?>("redirect_uri", $"http://{context.Request.Host}/token"),
            ]);
            var response = client.PostAsync("token", content).Result;
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = response.Content.ReadAsStringAsync().Result;
                return Results.Text("Error", "text/html");
            }
            var tokenResponse = response.Content.ReadFromJsonAsync<TokenResponse>().Result;
            if (tokenResponse == null)
            {
                return Results.Text("Error", "text/html");
            }
            await chatService.StartAsync(tokenResponse.access_token, context.RequestAborted);
            clipService.Start(tokenResponse.access_token);

            monitorService.Start(tokenResponse.access_token);

            // return redirect
            return Results.Redirect("/");
        });

        app.MapGet("/pubsubcallback", (
            HttpContext context,
            IWebsocketService websocketService
        ) =>
        {
            var authorizationCode = context.Request.Query["code"];

            // Use the authorization code to get the access token from https://id.twitch.tv/oauth2/token with a http post
            HttpClient client = new()
            {
                BaseAddress = new Uri("https://id.twitch.tv/oauth2/")
            };
            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string?, string?>("client_id", clientId),
                new KeyValuePair<string?, string?>("client_secret", clientSecret),
                new KeyValuePair<string?, string?>("code", authorizationCode),
                new KeyValuePair<string?, string?>("grant_type", "authorization_code"),
                new KeyValuePair<string?, string?>("redirect_uri", $"http://{context.Request.Host}/token"),
            ]);
            var response = client.PostAsync("token", content).Result;
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = response.Content.ReadAsStringAsync().Result;
                return Results.Text("Error", "text/html");
            }
            var tokenResponse = response.Content.ReadFromJsonAsync<TokenResponse>().Result;
            if (tokenResponse == null)
            {
                return Results.Text("Error", "text/html");
            }

            websocketService.StartAsync(tokenResponse.access_token);

            return Results.Redirect("/");
        });

        return app;
    }

    private static string BuildScopes(string[] scopes)
    {
        // Url encode the scopes string
        return HttpUtility.UrlEncode(string.Join(" ", scopes));
    }
}

public record TokenResponse(string access_token, string refresh_token, string token_type, int expires_in, string[] scope);
