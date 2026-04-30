using Leontes.Api.Endpoints;
using Leontes.Api.Extensions;
using Leontes.Application;
using Leontes.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddApiLogging();

builder.Services.AddApiDiagnostics();
builder.Services.AddApiCors();
builder.Services.AddApiRateLimiting();
builder.Services.AddApiKeyAuthentication(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddStructuralVision(builder.Configuration);

builder.Services.AddApiHealthChecks(builder.Configuration);

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseApiDiagnostics();
app.UseApiLogging();
app.UseApiCors();
app.UseApiRateLimiting();
app.UseApiAuthentication();

app.MapApiDocs();
app.MapApiHealthChecks();
app.MapApiEndpoints();

app.Run();
