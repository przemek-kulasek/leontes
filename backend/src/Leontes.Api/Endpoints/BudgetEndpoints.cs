using Leontes.Application.CostControl;
using Leontes.Domain.Entities;
using Leontes.Domain.Exceptions;

namespace Leontes.Api.Endpoints;

public static class BudgetEndpoints
{
    public static RouteGroupBuilder MapBudgetEndpoints(this RouteGroupBuilder group)
    {
        var budget = group.MapGroup("/budget").WithTags("Budget");

        budget.MapGet("/today", GetToday)
            .WithName("GetBudgetToday")
            .WithSummary("Get today's token usage and budget status")
            .Produces<DailyBudgetReport>();

        budget.MapGet("/history", GetHistory)
            .WithName("GetBudgetHistory")
            .WithSummary("Get token usage history for the last N days")
            .Produces<IReadOnlyList<DailyBudgetReport>>();

        budget.MapGet("/policy", GetPolicy)
            .WithName("GetBudgetPolicy")
            .WithSummary("Get the current budget policy")
            .Produces<BudgetPolicy>();

        budget.MapPut("/policy", UpdatePolicy)
            .WithName("UpdateBudgetPolicy")
            .WithSummary("Replace the current budget policy")
            .Produces<BudgetPolicy>();

        return group;
    }

    private static async Task<IResult> GetToday(
        ICostDashboard dashboard,
        CancellationToken cancellationToken) =>
        Results.Ok(await dashboard.GetTodayAsync(cancellationToken));

    private static async Task<IResult> GetHistory(
        ICostDashboard dashboard,
        int days = 7,
        CancellationToken cancellationToken = default)
    {
        if (days <= 0 || days > 365)
            throw new ValidationException("'days' must be between 1 and 365.");

        return Results.Ok(await dashboard.GetHistoryAsync(days, cancellationToken));
    }

    private static async Task<IResult> GetPolicy(
        IBudgetPolicyStore store,
        CancellationToken cancellationToken) =>
        Results.Ok(await store.GetAsync(cancellationToken));

    private static async Task<IResult> UpdatePolicy(
        BudgetPolicyRequest request,
        IBudgetPolicyStore store,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var policy = new BudgetPolicy
        {
            DailyTokenBudget = request.DailyTokenBudget,
            WarningThresholdPercent = request.WarningThresholdPercent,
            ThrottleThresholdPercent = request.ThrottleThresholdPercent,
            HardStopEnabled = request.HardStopEnabled,
            HardStopThresholdPercent = request.HardStopThresholdPercent,
            FeatureAllocations = new Dictionary<string, int>(request.FeatureAllocations)
        };

        await store.UpdateAsync(policy, cancellationToken);
        return Results.Ok(await store.GetAsync(cancellationToken));
    }

    private static void Validate(BudgetPolicyRequest request)
    {
        if (request.DailyTokenBudget <= 0)
            throw new ValidationException("DailyTokenBudget must be greater than 0.");

        if (request.WarningThresholdPercent <= 0 || request.WarningThresholdPercent >= request.ThrottleThresholdPercent)
            throw new ValidationException("WarningThresholdPercent must be > 0 and < ThrottleThresholdPercent.");

        if (request.ThrottleThresholdPercent >= request.HardStopThresholdPercent)
            throw new ValidationException("ThrottleThresholdPercent must be < HardStopThresholdPercent.");

        if (request.FeatureAllocations is null || request.FeatureAllocations.Count == 0)
            throw new ValidationException("FeatureAllocations must contain at least one entry.");

        if (request.FeatureAllocations.Values.Any(v => v < 0))
            throw new ValidationException("FeatureAllocations must be non-negative.");
    }
}

public sealed record BudgetPolicyRequest(
    int DailyTokenBudget,
    int WarningThresholdPercent,
    int ThrottleThresholdPercent,
    bool HardStopEnabled,
    int HardStopThresholdPercent,
    Dictionary<string, int> FeatureAllocations);
