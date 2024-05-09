using Application.Infrastructure;
using Serilog;
using Host.Endpoints;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Host.UseSerilog((hostContext, provider, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(hostContext.Configuration)
            .ReadFrom.Services(provider)
            .Enrich.FromLogContext();
    });

    var app = builder.Build();

    app.UseHttpsRedirection();
    app.MapEndpoints(builder.Configuration);

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "An unhandled exception occured during bootstrapping");
}
finally
{
    Log.CloseAndFlush();
}
