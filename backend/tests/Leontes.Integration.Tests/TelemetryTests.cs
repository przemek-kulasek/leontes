using Leontes.Application.Telemetry;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Domain.Exceptions;
using Leontes.Infrastructure.Data;
using Leontes.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Leontes.Integration.Tests;

public sealed class TelemetryTests(LeontesApiFactory factory)
    : IClassFixture<LeontesApiFactory>
{
    private readonly LeontesApiFactory _factory = factory;

    [Fact]
    public async Task ExplainAsync_UnknownRequest_ThrowsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExplainabilityService>();

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.ExplainAsync(Guid.NewGuid(), ct));
    }

    [Fact]
    public async Task ExplainAsync_StoredTrace_RendersStageSummary()
    {
        var ct = TestContext.Current.CancellationToken;
        var requestId = Guid.NewGuid();

        await using (var writeScope = _factory.Services.CreateAsyncScope())
        {
            var db = writeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var trace = new PipelineTrace
            {
                Id = Guid.NewGuid(),
                RequestId = requestId,
                ConversationId = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow.AddSeconds(-5),
                CompletedAt = DateTime.UtcNow,
                Outcome = PipelineOutcome.Success,
                ConfidenceOverall = 0.82
            };
            trace.Stages.Add(new StageTrace
            {
                Id = Guid.NewGuid(),
                PipelineTraceId = trace.Id,
                StageName = "Plan",
                StartedAt = trace.StartedAt,
                CompletedAt = trace.StartedAt.AddMilliseconds(120),
                Outcome = StageOutcome.Success,
                PipelineTrace = trace
            });
            db.PipelineTraceSet.Add(trace);
            await db.SaveChangesAsync(ct);
        }

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExplainabilityService>();

        var explanation = await service.ExplainAsync(requestId, ct);

        Assert.Contains("Plan", explanation);
        Assert.Contains("0.82", explanation);
    }

    [Fact]
    public async Task GetRecentTracesAsync_ReturnsAccurateTotalCount()
    {
        var ct = TestContext.Current.CancellationToken;
        int seededTraces;

        await using (var writeScope = _factory.Services.CreateAsyncScope())
        {
            var db = writeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var existing = await db.PipelineTraceSet.CountAsync(ct);

            for (var i = 0; i < 3; i++)
            {
                db.PipelineTraceSet.Add(new PipelineTrace
                {
                    Id = Guid.NewGuid(),
                    RequestId = Guid.NewGuid(),
                    ConversationId = Guid.NewGuid(),
                    StartedAt = DateTime.UtcNow.AddSeconds(-i),
                    Outcome = PipelineOutcome.Success
                });
            }
            await db.SaveChangesAsync(ct);
            seededTraces = existing + 3;
        }

        using var scope = _factory.Services.CreateScope();
        var collector = scope.ServiceProvider.GetRequiredService<ITelemetryCollector>();

        var paged = await collector.GetRecentTracesAsync(page: 1, pageSize: 2, ct);

        Assert.Equal(2, paged.Items.Count);
        Assert.Equal(seededTraces, paged.TotalCount);
    }
}
