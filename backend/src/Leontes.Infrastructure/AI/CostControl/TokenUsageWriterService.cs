using Leontes.Application.CostControl;
using Leontes.Domain.Entities;
using Leontes.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure.AI.CostControl;

internal sealed class TokenUsageWriterService(
    TokenUsageChannel channel,
    IServiceScopeFactory scopeFactory,
    ILogger<TokenUsageWriterService> logger) : BackgroundService
{
    private const int BatchSize = 64;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = channel.Channel.Reader;

        while (await reader.WaitToReadAsync(stoppingToken))
        {
            var batch = new List<TokenUsage>(BatchSize);

            while (batch.Count < BatchSize && reader.TryRead(out var usage))
                batch.Add(usage);

            try
            {
                await PersistAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist token usage batch of size {Count}", batch.Count);
            }
        }
    }

    private async Task PersistAsync(IReadOnlyList<TokenUsage> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var usage in batch)
        {
            db.TokenUsageRecordSet.Add(new TokenUsageRecord
            {
                Id = Guid.NewGuid(),
                Feature = usage.Feature,
                Operation = usage.Operation,
                ModelId = usage.ModelId,
                InputTokens = usage.InputTokens,
                OutputTokens = usage.OutputTokens,
                Timestamp = usage.Timestamp
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
