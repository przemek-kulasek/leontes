using Leontes.Application;
using Leontes.Application.Chat;
using Leontes.Application.Configuration;
using Leontes.Application.Messaging;
using Leontes.Application.ProactiveCommunication;
using Leontes.Application.ThinkingPipeline;
using Leontes.Infrastructure.AI;
using Leontes.Infrastructure.AI.Memory;
using Leontes.Infrastructure.AI.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Executors;
using Leontes.Infrastructure.AI.Tools;
using Leontes.Infrastructure.Data;
using Leontes.Infrastructure.Data.Interceptors;
using Leontes.Infrastructure.ProactiveCommunication;
using Leontes.Infrastructure.Signal;
using Leontes.Infrastructure.Telegram;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector());
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<ApplicationDbContextInitializer>();

        AddAiServices(services, configuration);
        AddMemoryServices(services, configuration);
        AddThinkingPipeline(services, configuration);
        AddSignalServices(services, configuration);
        AddTelegramServices(services, configuration);
        AddProactiveCommunicationServices(services, configuration);

        return services;
    }

    private static void AddMemoryServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MemoryOptions>(configuration.GetSection(MemoryOptions.SectionName));

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MemoryOptions>>().Value;
            var endpoint = options.EmbeddingEndpoint
                ?? configuration["AiProvider:Endpoint"]
                ?? "http://localhost:11434";

            return options.EmbeddingProvider.ToLowerInvariant() switch
            {
                "ollama" => new OllamaApiClient(new Uri(endpoint), options.EmbeddingModelId),
                _ => throw new InvalidOperationException(
                    $"Unsupported embedding provider '{options.EmbeddingProvider}'. Supported values: Ollama.")
            };
        });

        services.AddSingleton<IEmbeddingService, EmbeddingService>();
        services.AddSingleton<IMemoryStore, PgVectorMemoryStore>();
        services.AddSingleton<ISynapseGraph, PgVectorSynapseGraph>();
    }

    private static void AddAiServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PersonaOptions>(configuration.GetSection(PersonaOptions.SectionName));
        services.Configure<AiProviderOptions>(configuration.GetSection(AiProviderOptions.SectionName));

        var modelsSection = configuration.GetSection("AiProvider:Models");

        // Keyed IChatClient instances: Large and Small
        services.AddKeyedSingleton<IChatClient>("Large", (_, _) =>
            CreateChatClient(modelsSection.GetSection("Large"), configuration));

        services.AddKeyedSingleton<IChatClient>("Small", (_, _) =>
            CreateChatClient(modelsSection.GetSection("Small"), configuration));

        // Default (unkeyed) resolves to Large for backwards compatibility
        services.AddSingleton<IChatClient>(sp =>
            sp.GetRequiredKeyedService<IChatClient>("Large"));

        // Load persona instructions from the output directory (CopyToOutputDirectory)
        var personaFile = configuration["Persona:InstructionsFile"] ?? "persona.md";
        var personaPath = Path.Combine(AppContext.BaseDirectory, personaFile);
        var instructions = File.Exists(personaPath)
            ? File.ReadAllText(personaPath)
            : "You are Leontes, a helpful personal AI assistant. Be concise and accurate.";

        services.AddSingleton(new PersonaInstructions(instructions));

        services.AddSingleton<AIAgent>(sp =>
            new ChatClientAgent(
                sp.GetRequiredKeyedService<IChatClient>("Large"),
                instructions: sp.GetRequiredService<PersonaInstructions>().Instructions,
                name: "Leontes",
                tools: [AIFunctionFactory.Create(CurrentDateTimeTool.GetCurrentDateTime)],
                loggerFactory: sp.GetService<ILoggerFactory>()));

        services.AddScoped<IChatService, ChatService>();
    }

    private static IChatClient CreateChatClient(IConfigurationSection modelSection, IConfiguration root)
    {
        var provider = modelSection["Provider"]
            ?? root["AiProvider:Provider"]
            ?? "Ollama";
        var endpoint = modelSection["Endpoint"]
            ?? root["AiProvider:Endpoint"]
            ?? "http://localhost:11434";
        var modelId = modelSection["ModelId"]
            ?? root["AiProvider:Model"]
            ?? "qwen2.5:7b";

        return provider.ToLowerInvariant() switch
        {
            "ollama" => new OllamaApiClient(new Uri(endpoint), modelId),
            _ => throw new InvalidOperationException(
                $"Unsupported AI provider '{provider}'. Supported values: Ollama.")
        };
    }

    private static void AddThinkingPipeline(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ThinkingPipelineOptions>(
            configuration.GetSection(ThinkingPipelineOptions.SectionName));

        // Stub implementations (replaced when real features are built)
        services.AddSingleton<ITokenMeter, NullTokenMeter>();
        services.AddSingleton<IDecisionRecorder, NullDecisionRecorder>();

        // Checkpoint store (in-memory for now)
        services.AddSingleton<JsonCheckpointStore, InMemoryCheckpointStore>();
        services.AddSingleton(sp =>
            CheckpointManager.CreateJson(sp.GetRequiredService<JsonCheckpointStore>()));

        // Executors
        services.AddSingleton<PerceiveExecutor>();
        services.AddSingleton<EnrichExecutor>();
        services.AddSingleton<PlanExecutor>();
        services.AddSingleton<PlanResumeExecutor>();
        services.AddSingleton<ExecuteExecutor>();
        services.AddSingleton<ReflectExecutor>();

        // Workflow definition
        services.AddSingleton<Workflow>(sp =>
            ThinkingWorkflowDefinition.Build(
                sp.GetRequiredService<PerceiveExecutor>(),
                sp.GetRequiredService<EnrichExecutor>(),
                sp.GetRequiredService<PlanExecutor>(),
                sp.GetRequiredService<PlanResumeExecutor>(),
                sp.GetRequiredService<ExecuteExecutor>(),
                sp.GetRequiredService<ReflectExecutor>()));

        // Workflow host
        services.AddSingleton<ThinkingWorkflowHost>();
    }

    private static void AddSignalServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SignalOptions>(configuration.GetSection(SignalOptions.SectionName));

        services.AddHttpClient<SignalRestClient>(client =>
        {
            var baseUrl = configuration[$"{SignalOptions.SectionName}:BaseUrl"] ?? "http://localhost:8081";
            client.BaseAddress = new Uri(baseUrl);
        })
        .AddStandardResilienceHandler();

        services.AddSingleton<IMessagingClient>(sp => sp.GetRequiredService<SignalRestClient>());
    }

    private static void AddProactiveCommunicationServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ProactiveCommunicationOptions>(
            configuration.GetSection(ProactiveCommunicationOptions.SectionName));

        services.AddSingleton<WorkflowEventBridge>();
        services.AddSingleton<IWorkflowEventBridge>(sp =>
            sp.GetRequiredService<WorkflowEventBridge>());

        services.AddSingleton<IWorkflowSessionManager, WorkflowSessionManager>();
        services.AddScoped<IProactiveEventStore, ProactiveEventStore>();
    }

    private static void AddTelegramServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));

        var pollTimeout = configuration.GetValue($"{TelegramOptions.SectionName}:PollTimeoutSeconds", 30);

        // No AddStandardResilienceHandler — Telegram long-polling holds the connection open
        // for up to PollTimeoutSeconds (default 30s), which conflicts with the standard 10s
        // attempt timeout. The bridge service handles errors and retries at the application level.
        services.AddHttpClient<TelegramBotClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.telegram.org");
            client.Timeout = TimeSpan.FromSeconds(pollTimeout + 10);
        });

        services.AddSingleton<IMessagingClient>(sp => sp.GetRequiredService<TelegramBotClient>());
    }
}
