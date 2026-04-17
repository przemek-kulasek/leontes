using Leontes.Application.ThinkingPipeline;
using Leontes.Infrastructure.Data;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Leontes.Integration.Tests.Infrastructure;

public sealed class LeontesApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .Build();

    public const string TestApiKey = "lnt_test-api-key-for-integration-tests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            _postgres.GetConnectionString());

        builder.UseSetting("Authentication:ApiKey", TestApiKey);

        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            services.AddDbContext<ApplicationDbContext>((sp, opt) =>
            {
                opt.AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor>());
                opt.UseNpgsql(_postgres.GetConnectionString(), npgsql => npgsql.UseVector());
            });

            // Replace all IChatClient registrations (keyed and non-keyed)
            // so pipeline executors using [FromKeyedServices("Large"/"Small")]
            // get the test client instead of a real Ollama connection.
            var testClient = new TestChatClient();

            services.RemoveAll<IChatClient>();
            services.AddKeyedSingleton<IChatClient>("Large", testClient);
            services.AddKeyedSingleton<IChatClient>("Small", testClient);
            services.AddSingleton<IChatClient>(testClient);

            services.RemoveAll<IEmbeddingService>();
            services.AddSingleton<IEmbeddingService, FakeEmbeddingService>();

            services.RemoveAll<AIAgent>();
            services.AddSingleton<AIAgent>(sp => new ChatClientAgent(
                sp.GetRequiredService<IChatClient>(),
                instructions: "You are a test assistant.",
                name: "TestAgent"));
        });

        builder.UseEnvironment("Development");
    }

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
