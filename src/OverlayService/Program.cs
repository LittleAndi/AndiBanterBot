using OverlayService;
using OverlayService.Overlays;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddOverlays();

var app = builder.Build();

// Exposes /health and /alive so the Aspire dashboard can report this service's liveness.
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Serves each module's static bundle at /overlays/{slug}/... and the shared
// helpers at /overlays/_shared/... . Overlays are plain HTML/CSS/JS (not Blazor)
// so animation stays client-driven and smooth inside an OBS browser source.
app.UseStaticFiles();

// Registry listing consumed by Web's overlay picker page. Empty until a real
// module registers itself from its own future issue.
app.MapGet("/overlays", (IOverlayRegistry registry) =>
    Results.Ok(registry.List()
        .Select(m => new OverlayModuleResponse(m.Slug, m.DisplayName, m.Description, m.DefaultWidth, m.DefaultHeight))
        .ToArray()));

// Browser-source entrypoint: serves wwwroot/{AssetBundlePath}/index.html for a
// registered slug. Per-instance config rides on the query string (?theme=...),
// read client-side by overlay-client.js — nothing is persisted server-side.
//
// Reserved convention (not implemented here — nothing consumes it yet): a module
// that needs data exposes it at /overlay/{slug}/events, polled by the shared
// client helper.
app.MapGet("/overlay/{slug}", (string slug, IOverlayRegistry registry, IWebHostEnvironment env) =>
{
    if (!registry.TryGet(slug, out var module))
    {
        return Results.NotFound();
    }

    var indexPath = Path.Combine(env.WebRootPath, module.AssetBundlePath, "index.html");
    return File.Exists(indexPath)
        ? Results.File(indexPath, "text/html")
        : Results.NotFound();
});

app.MapPubgStatsOverlay();

app.Run();

public record OverlayModuleResponse(string Slug, string DisplayName, string Description, int DefaultWidth, int DefaultHeight);
