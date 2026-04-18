using HealthChecks.UI.Client;
using Leontes.Api;
using Leontes.Api.Endpoints;
using Leontes.Api.Extensions;
using Leontes.Application;
using Leontes.Infrastructure;
using Leontes.Api.HealthChecks;
using Leontes.Infrastructure.AI;
using Leontes.Infrastructure.AI.Memory;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

builder.Services.AddApiCors();
builder.Services.AddApiRateLimiting();
builder.Services.AddApiKeyAuthentication(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<MemoryConsolidationService>();
builder.Services.AddHostedService<DegradedModeMonitor>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "database")
    .AddCheck<LlmProviderHealthCheck>("llm-provider")
    .AddCheck<ProcessingQueueHealthCheck>("processing-queue");

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseApiCors();
app.UseApiRateLimiting();
app.UseApiAuthentication();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/_health", new()
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapApiEndpoints();

app.Run();

public partial class Program;
