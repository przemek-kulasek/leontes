using Leontes.Application.CostControl;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.AI.CostControl;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leontes.Infrastructure.Tests.AI.CostControl;

public sealed class ModelRouterTests
{
    [Fact]
    public async Task SelectModelAsync_WhenBudgetNormal_ReturnsPreferredTier()
    {
        var router = Build(BudgetState.Normal);

        var selection = await router.SelectModelAsync(
            new ModelRoutingContext(CostControlFeatures.Chat, "Plan", ModelTier.Large),
            CancellationToken.None);

        Assert.Equal(ModelTier.Large, selection.Tier);
        Assert.False(selection.Downgraded);
    }

    [Fact]
    public async Task SelectModelAsync_WhenWarningAndBackground_DowngradesBackground()
    {
        var router = Build(BudgetState.Warning);

        var selection = await router.SelectModelAsync(
            new ModelRoutingContext(CostControlFeatures.Consolidation, "Summarize", ModelTier.Large),
            CancellationToken.None);

        Assert.Equal(ModelTier.Small, selection.Tier);
        Assert.True(selection.Downgraded);
    }

    [Fact]
    public async Task SelectModelAsync_WhenWarningAndInteractive_KeepsPreferredTier()
    {
        var router = Build(BudgetState.Warning);

        var selection = await router.SelectModelAsync(
            new ModelRoutingContext(CostControlFeatures.Chat, "Execute", ModelTier.Large),
            CancellationToken.None);

        Assert.Equal(ModelTier.Large, selection.Tier);
        Assert.False(selection.Downgraded);
    }

    [Fact]
    public async Task SelectModelAsync_WhenThrottled_DowngradesInteractive()
    {
        var router = Build(BudgetState.Throttled);

        var selection = await router.SelectModelAsync(
            new ModelRoutingContext(CostControlFeatures.Chat, "Execute", ModelTier.Large),
            CancellationToken.None);

        Assert.Equal(ModelTier.Small, selection.Tier);
        Assert.True(selection.Downgraded);
    }

    private static ModelRouter Build(BudgetState state) =>
        new(new FakeLedger(state), NullLogger<ModelRouter>.Instance);

    private sealed class FakeLedger(BudgetState state) : ITokenLedger
    {
        public Task RecordUsageAsync(TokenUsage usage, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<TokenBudgetStatus> GetGlobalBudgetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new TokenBudgetStatus("Global", 0, 1000, PercentFor(state), state));

        public Task<TokenBudgetStatus> GetFeatureBudgetStatusAsync(string feature, CancellationToken cancellationToken) =>
            Task.FromResult(new TokenBudgetStatus(feature, 0, 1000, PercentFor(state), state));

        public Task<DailyBudgetReport> GetTodayAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DailyBudgetReport(DateTime.UtcNow, 0, 1000, 0,
                new Dictionary<string, FeatureUsageReport>(), null));

        public Task<IReadOnlyList<DailyBudgetReport>> GetHistoryAsync(int days, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DailyBudgetReport>>(Array.Empty<DailyBudgetReport>());

        private static double PercentFor(BudgetState s) => s switch
        {
            BudgetState.Normal => 10,
            BudgetState.Warning => 80,
            BudgetState.Throttled => 95,
            BudgetState.Exhausted => 105,
            _ => 0
        };
    }
}
