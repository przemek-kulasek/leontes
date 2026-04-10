using Leontes.Infrastructure.Data;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Leontes.Integration.Tests.Infrastructure;

public sealed class LeontesApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            services.AddDbContext<ApplicationDbContext>((sp, opt) =>
            {
                opt.AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor>());
                opt.UseNpgsql(_postgres.GetConnectionString());
            });

            var chatClientDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IChatClient) && !d.IsKeyedService);

            if (chatClientDescriptor is not null)
                services.Remove(chatClientDescriptor);

            services.AddSingleton<IChatClient>(new TestChatClient());

            var agentDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AIAgent));

            if (agentDescriptor is not null)
                services.Remove(agentDescriptor);

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
