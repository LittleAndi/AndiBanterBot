using Application.Infrastructure.Twitch;

namespace Host.Endpoints;
public static class Endpoints
{
    public static WebApplication MapEndpoints(this WebApplication app, IConfiguration configuration)
    {
        var clientId = configuration["TwitchLib:ClientId"];
        var clientSecret = configuration["TwitchLib:ClientSecret"];

        app.MapGet("/", () =>
        {
            // Return HTML with hello world
            return Results.Text($"<a href='https://id.twitch.tv/oauth2/authorize?response_type=code&client_id={clientId}&redirect_uri=http%3A%2F%2Flocalhost%3A5000%2Fcallback&scope=user%3Abot%20channel%3Abot%20chat%3Aedit%20chat%3Aread%20whispers%3Aread%20clips%3Aedit'>Login</a>", "text/html");
        });

        app.MapGet("/callback", async (HttpContext context, IChatService chatService, IClipService clipService) =>
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

            // return redirect
            return Results.Redirect("/");
        });

        return app;
    }
}

public record TokenResponse(string access_token, string refresh_token, string token_type, int expires_in, string[] scope);
