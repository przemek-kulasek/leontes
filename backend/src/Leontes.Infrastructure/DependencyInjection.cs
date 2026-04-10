using Leontes.Application;
using Leontes.Application.Chat;
using Leontes.Infrastructure.AI;
using Leontes.Infrastructure.AI.Tools;
using Leontes.Infrastructure.Data;
using Leontes.Infrastructure.Data.Interceptors;
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

        return services;
    }

    private static void AddAiServices(IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["AiProvider:Endpoint"] ?? "http://localhost:11434";
        var model = configuration["AiProvider:Model"] ?? "qwen2.5:7b";

        services.AddSingleton<IChatClient>(_ =>
            new OllamaApiClient(new Uri(endpoint), model));

        services.AddSingleton<AIAgent>(sp =>
            new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                instructions: "You are Leontes, a helpful personal AI assistant. Be concise and accurate. Use available tools when they can help answer the user's question.",
                name: "Leontes",
                tools: [AIFunctionFactory.Create(CurrentDateTimeTool.GetCurrentDateTime)],
                loggerFactory: sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()));

        services.AddScoped<IChatService, ChatService>();
    }
}
