using Leontes.Application.Configuration;
using Leontes.Application.CostControl;
using Leontes.Domain.Entities;
using Leontes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.CostControl;

internal sealed class BudgetPolicyStore(
    ApplicationDbContext db,
    IOptions<CostControlOptions> options,
    ILogger<BudgetPolicyStore> logger) : IBudgetPolicyStore
{
    public async Task<BudgetPolicy> GetAsync(CancellationToken cancellationToken)
    {
        var policy = await db.BudgetPolicySet
            .AsNoTracking()
            .OrderBy(p => p.Created)
            .FirstOrDefaultAsync(cancellationToken);

        if (policy is not null)
            return NormalizeAllocations(policy);

        var seeded = FromOptions(options.Value);
        db.BudgetPolicySet.Add(seeded);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded default BudgetPolicy with daily budget {Budget}", seeded.DailyTokenBudget);
        return NormalizeAllocations(seeded);
    }

    public async Task UpdateAsync(BudgetPolicy policy, CancellationToken cancellationToken)
    {
        var existing = await db.BudgetPolicySet
            .OrderBy(p => p.Created)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            db.BudgetPolicySet.Add(policy);
        }
        else
        {
            existing.DailyTokenBudget = policy.DailyTokenBudget;
            existing.WarningThresholdPercent = policy.WarningThresholdPercent;
            existing.ThrottleThresholdPercent = policy.ThrottleThresholdPercent;
            existing.HardStopEnabled = policy.HardStopEnabled;
            existing.HardStopThresholdPercent = policy.HardStopThresholdPercent;
            existing.FeatureAllocations = policy.FeatureAllocations;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static BudgetPolicy FromOptions(CostControlOptions options) => new()
    {
        Id = Guid.NewGuid(),
        DailyTokenBudget = options.DailyTokenBudget,
        WarningThresholdPercent = options.WarningThresholdPercent,
        ThrottleThresholdPercent = options.ThrottleThresholdPercent,
        HardStopEnabled = options.HardStopEnabled,
        HardStopThresholdPercent = options.HardStopThresholdPercent,
        FeatureAllocations = new Dictionary<string, int>(options.FeatureAllocations)
    };

    private static BudgetPolicy NormalizeAllocations(BudgetPolicy policy)
    {
        var total = policy.FeatureAllocations.Values.Sum();
        if (total == 100 || total == 0)
            return policy;

        var normalized = new Dictionary<string, int>(policy.FeatureAllocations.Count);
        foreach (var (key, value) in policy.FeatureAllocations)
            normalized[key] = (int)Math.Round(value * 100.0 / total);

        policy.FeatureAllocations = normalized;
        return policy;
    }
}
