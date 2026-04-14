using Leontes.Application;
using Leontes.Application.Chat;
using Leontes.Application.Signal;
using Leontes.Infrastructure.AI;
using Leontes.Infrastructure.AI.Tools;
using Leontes.Infrastructure.Data;
using Leontes.Infrastructure.Data.Interceptors;
using Leontes.Infrastructure.Signal;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace Leontes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<AuditInterceptor>();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<ApplicationDbContextInitializer>();

        AddAiServices(services, configuration);
        AddSignalServices(services, configuration);

        return services;
    }

    private static void AddAiServices(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["AiProvider:Provider"] ?? "Ollama";
        var endpoint = configuration["AiProvider:Endpoint"] ?? "http://localhost:11434";
        var model = configuration["AiProvider:Model"] ?? "qwen2.5:7b";

        services.AddSingleton<IChatClient>(_ => provider.ToLowerInvariant() switch
        {
            "ollama" => new OllamaApiClient(new Uri(endpoint), model),
            _ => throw new InvalidOperationException(
                $"Unsupported AI provider '{provider}'. Supported values: Ollama.")
        });

        services.AddSingleton<AIAgent>(sp =>
            new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                instructions: "You are Leontes, a helpful personal AI assistant. Be concise and accurate. Use available tools when they can help answer the user's question.",
                name: "Leontes",
                tools: [AIFunctionFactory.Create(CurrentDateTimeTool.GetCurrentDateTime)],
                loggerFactory: sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()));

        services.AddScoped<IChatService, ChatService>();
    }

    private static void AddSignalServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SignalOptions>(configuration.GetSection(SignalOptions.SectionName));

        services.AddHttpClient<ISignalClient, SignalRestClient>(client =>
        {
            var baseUrl = configuration[$"{SignalOptions.SectionName}:BaseUrl"] ?? "http://localhost:8081";
            client.BaseAddress = new Uri(baseUrl);
        })
        .AddStandardResilienceHandler();
    }
}
