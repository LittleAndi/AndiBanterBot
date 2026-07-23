using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var environment = builder.Environment.EnvironmentName;

builder.Configuration.AddJsonFile("appsettings.prompts.json", optional: true, reloadOnChange: true);

builder.AddProject<Projects.PubgService>("pubg")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", environment)
    .WithEnvironment("DOTNET_ENVIRONMENT", environment);

var twitch = builder.AddProject<Projects.TwitchService>("twitch")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", environment)
    .WithEnvironment("DOTNET_ENVIRONMENT", environment);

builder.AddProject<Projects.OverlayService>("overlays")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", environment)
    .WithEnvironment("DOTNET_ENVIRONMENT", environment);

builder.AddProject<Projects.Web>("web")
    .WithReference(twitch)
    .WithExternalHttpEndpoints();

builder.Build().Run();
