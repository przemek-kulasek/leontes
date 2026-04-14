using Leontes.Application;
using Leontes.Infrastructure;
using Leontes.Worker.Sentinel;
using Leontes.Worker.Signal;
using Leontes.Worker.Telegram;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddWindowsService(options =>
    options.ServiceName = "Leontes Worker");

builder.Logging.ClearProviders();
builder.Services.AddSerilog(configuration =>
    configuration.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpClient("LeontesApi", client =>
{
    var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5154";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
})
.AddStandardResilienceHandler();

builder.Services.AddHostedService<SentinelService>();
builder.Services.AddHostedService<SignalBridgeService>();
builder.Services.AddHostedService<TelegramBridgeService>();

var host = builder.Build();
host.Run();
