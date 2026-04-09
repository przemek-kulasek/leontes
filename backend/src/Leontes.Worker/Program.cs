using Leontes.Application;
using Leontes.Infrastructure;
using Leontes.Worker.Sentinel;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
    options.ServiceName = "Leontes Worker");

builder.Logging.ClearProviders();
builder.Services.AddSerilog(configuration =>
    configuration.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<SentinelService>();

var host = builder.Build();
host.Run();
