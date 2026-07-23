using Web.Components;
using Web.Components.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("twitch", (ServiceProvider, client) =>
{
    client.BaseAddress = new("https+http://twitch");
});
builder.Services.AddHttpClient("overlays", (ServiceProvider, client) =>
{
    client.BaseAddress = new("https+http://overlays");
});
builder.Services.AddTransient<TwitchApiClient>();
builder.Services.AddTransient<TwitchAuthUrlBuilder>();
builder.Services.AddTransient<OverlayApiClient>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
