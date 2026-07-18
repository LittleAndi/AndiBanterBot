var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPubg(builder.Configuration);
builder.Services.AddHostedService<PubgBackgroundService>();

IHost host = builder.Build();
host.Run();