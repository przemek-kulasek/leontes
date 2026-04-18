using Leontes.Application.Configuration;
using Leontes.Application.CostControl;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.CostControl;

internal sealed class TokenLedger(
    IServiceScopeFactory scopeFactory,
    IOptions<CostControlOptions> options) : ITokenLedger
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    public async Task RecordUsageAsync(TokenUsage usage, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

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

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TokenBudgetStatus> GetGlobalBudgetStatusAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var policyStore = scope.ServiceProvider.GetRequiredService<IBudgetPolicyStore>();

        var policy = await policyStore.GetAsync(cancellationToken);
        var cutoff = DateTime.UtcNow - Window;

        var used = await db.TokenUsageRecordSet
            .AsNoTracking()
            .Where(r => r.Timestamp >= cutoff)
            .SumAsync(r => (int?)(r.InputTokens + r.OutputTokens), cancellationToken) ?? 0;

        return BuildStatus("Global", used, policy.DailyTokenBudget, policy);
    }

    public async Task<TokenBudgetStatus> GetFeatureBudgetStatusAsync(string feature, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var policyStore = scope.ServiceProvider.GetRequiredService<IBudgetPolicyStore>();

        var policy = await policyStore.GetAsync(cancellationToken);
        var cutoff = DateTime.UtcNow - Window;

        var used = await db.TokenUsageRecordSet
            .AsNoTracking()
            .Where(r => r.Timestamp >= cutoff && r.Feature == feature)
            .SumAsync(r => (int?)(r.InputTokens + r.OutputTokens), cancellationToken) ?? 0;

        var allocationPercent = policy.FeatureAllocations.GetValueOrDefault(feature, 0);
        var featureBudget = (int)Math.Round(policy.DailyTokenBudget * (allocationPercent / 100.0));

        return BuildStatus(feature, used, featureBudget, policy);
    }

    public async Task<DailyBudgetReport> GetTodayAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var policy = await scope.ServiceProvider.GetRequiredService<IBudgetPolicyStore>().GetAsync(cancellationToken);

        var start = DateTime.UtcNow.Date;
        return await BuildReportAsync(db, start, start.AddDays(1), policy, cancellationToken);
    }

    public async Task<IReadOnlyList<DailyBudgetReport>> GetHistoryAsync(int days, CancellationToken cancellationToken)
    {
        if (days <= 0)
            return Array.Empty<DailyBudgetReport>();

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var policy = await scope.ServiceProvider.GetRequiredService<IBudgetPolicyStore>().GetAsync(cancellationToken);

        var results = new List<DailyBudgetReport>(days);
        var today = DateTime.UtcNow.Date;

        for (var offset = 0; offset < days; offset++)
        {
            var start = today.AddDays(-offset);
            results.Add(await BuildReportAsync(db, start, start.AddDays(1), policy, cancellationToken));
        }

        return results;
    }

    private async Task<DailyBudgetReport> BuildReportAsync(
        ApplicationDbContext db,
        DateTime start,
        DateTime end,
        BudgetPolicy policy,
        CancellationToken cancellationToken)
    {
        var records = await db.TokenUsageRecordSet
            .AsNoTracking()
            .Where(r => r.Timestamp >= start && r.Timestamp < end)
            .ToListAsync(cancellationToken);

        var byFeature = records
            .GroupBy(r => r.Feature)
            .ToDictionary(
                g => g.Key,
                g => new FeatureUsageReport(
                    g.Key,
                    g.Sum(r => r.InputTokens),
                    g.Sum(r => r.OutputTokens),
                    g.Count(),
                    g.GroupBy(r => r.ModelId).OrderByDescending(m => m.Count()).First().Key));

        var total = records.Sum(r => r.InputTokens + r.OutputTokens);
        var percent = policy.DailyTokenBudget <= 0 ? 0 : total * 100.0 / policy.DailyTokenBudget;

        var cost = ComputeCost(records, options.Value);

        return new DailyBudgetReport(start, total, policy.DailyTokenBudget, percent, byFeature, cost);
    }

    private static decimal? ComputeCost(IReadOnlyList<TokenUsageRecord> records, CostControlOptions options)
    {
        if (options.ModelCosts.Count == 0)
            return null;

        decimal total = 0;
        var hasAny = false;
        foreach (var r in records)
        {
            if (!options.ModelCosts.TryGetValue(r.ModelId, out var cost))
                continue;

            hasAny = true;
            total += r.InputTokens * cost.InputTokenCost + r.OutputTokens * cost.OutputTokenCost;
        }

        return hasAny ? total : null;
    }

    private static TokenBudgetStatus BuildStatus(string feature, int used, int budget, BudgetPolicy policy)
    {
        var percent = budget <= 0 ? 0 : used * 100.0 / budget;
        var state = percent switch
        {
            var p when p >= policy.HardStopThresholdPercent => BudgetState.Exhausted,
            var p when p >= policy.ThrottleThresholdPercent => BudgetState.Throttled,
            var p when p >= policy.WarningThresholdPercent => BudgetState.Warning,
            _ => BudgetState.Normal
        };
        return new TokenBudgetStatus(feature, used, budget, percent, state);
    }
}
