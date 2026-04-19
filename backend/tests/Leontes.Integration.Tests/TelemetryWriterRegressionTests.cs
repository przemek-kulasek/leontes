using Leontes.Application.Telemetry;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.Data;
using Leontes.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Leontes.Integration.Tests;

public sealed class TelemetryWriterRegressionTests(LeontesApiFactory factory)
    : IClassFixture<LeontesApiFactory>
{
    private readonly LeontesApiFactory _factory = factory;

    [Fact]
    public async Task Collector_EventsAcrossBatches_PersistsCompleteTrace()
    {
        // Repro: when events for the same RequestId arrive across multiple
        // drain batches, the second batch must be able to append new stages
        // to the already-persisted pipeline trace without a concurrency error.
        var ct = TestContext.Current.CancellationToken;
        var requestId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var collector = scope.ServiceProvider.GetRequiredService<ITelemetryCollector>();

            await collector.RecordPipelineStartAsync(requestId, conversationId, DateTime.UtcNow, ct);
            await collector.RecordStageStartAsync(requestId, "Perceive", DateTime.UtcNow, ct);
            await collector.RecordStageCompleteAsync(
                requestId, "Perceive", StageOutcome.Success, 10, 20, null, ct);

            await WaitForStageAsync(requestId, "Perceive", ct);

            await collector.RecordStageStartAsync(requestId, "Enrich", DateTime.UtcNow, ct);

            await WaitForStageAsync(requestId, "Enrich", ct);

            await collector.RecordStageCompleteAsync(
                requestId, "Enrich", StageOutcome.Success, 5, 15, null, ct);
            await collector.RecordPipelineCompleteAsync(
                requestId, PipelineOutcome.Success, confidence: null, ct);
        }

        var stored = await WaitForTraceAsync(requestId, ct);

        Assert.NotNull(stored);
        Assert.NotNull(stored.CompletedAt);
        Assert.Equal(2, stored.Stages.Count);
        Assert.All(stored.Stages, s => Assert.NotNull(s.CompletedAt));
    }

    private async Task WaitForStageAsync(Guid requestId, string stageName, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var exists = await db.PipelineTraceSet
                .AsNoTracking()
                .AnyAsync(t => t.RequestId == requestId
                    && t.Stages.Any(s => s.StageName == stageName), ct);
            if (exists)
                return;
            await Task.Delay(100, ct);
        }
        throw new TimeoutException($"Stage {stageName} for {requestId} never persisted.");
    }

    private async Task<Domain.Entities.PipelineTrace?> WaitForTraceAsync(
        Guid requestId, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var trace = await db.PipelineTraceSet
                .AsNoTracking()
                .Include(t => t.Stages)
                .FirstOrDefaultAsync(t => t.RequestId == requestId, ct);

            if (trace is not null && trace.CompletedAt is not null)
                return trace;

            await Task.Delay(200, ct);
        }

        return null;
    }
}
