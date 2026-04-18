using Leontes.Application.Configuration;
using Leontes.Application.CostControl;
using Leontes.Application.ProactiveCommunication;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.AI.CostControl;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.AI.CostControl;

public sealed class ThrottleEngineTests
{
    [Fact]
    public async Task EvaluateAsync_WhenNormal_AllowsInteractiveAndBackground()
    {
        var engine = Build(BudgetState.Normal);

        var chat = await engine.EvaluateAsync(CostControlFeatures.Chat, "Plan", CancellationToken.None);
        var background = await engine.EvaluateAsync(CostControlFeatures.Consolidation, "Summarize", CancellationToken.None);

        Assert.True(chat.Allowed);
        Assert.Null(chat.DelayBefore);
        Assert.True(background.Allowed);
        Assert.Null(background.DelayBefore);
    }

    [Fact]
    public async Task EvaluateAsync_WhenWarning_DelaysBackgroundButAllowsInteractive()
    {
        var engine = Build(BudgetState.Warning);

        var chat = await engine.EvaluateAsync(CostControlFeatures.Chat, "Plan", CancellationToken.None);
        var background = await engine.EvaluateAsync(CostControlFeatures.Consolidation, "Summarize", CancellationToken.None);

        Assert.True(chat.Allowed);
        Assert.NotNull(chat.UserNotice);

        Assert.True(background.Allowed);
        Assert.NotNull(background.DelayBefore);
    }

    [Fact]
    public async Task EvaluateAsync_WhenThrottled_DeniesBackgroundAndWarnsInteractive()
    {
        var engine = Build(BudgetState.Throttled);

        var chat = await engine.EvaluateAsync(CostControlFeatures.Chat, "Execute", CancellationToken.None);
        var background = await engine.EvaluateAsync(CostControlFeatures.ToolForge, "Generate", CancellationToken.None);

        Assert.True(chat.Allowed);
        Assert.NotNull(chat.UserNotice);

        Assert.False(background.Allowed);
        Assert.NotNull(background.DenialReason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenExhaustedWithHardStop_DeniesAllCalls()
    {
        var engine = Build(BudgetState.Exhausted, hardStop: true);

        var chat = await engine.EvaluateAsync(CostControlFeatures.Chat, "Execute", CancellationToken.None);
        var background = await engine.EvaluateAsync(CostControlFeatures.Consolidation, "Summarize", CancellationToken.None);

        Assert.False(chat.Allowed);
        Assert.False(background.Allowed);
    }

    [Fact]
    public async Task EvaluateAsync_WhenExhaustedWithoutHardStop_AllowsInteractive()
    {
        var engine = Build(BudgetState.Exhausted, hardStop: false);

        var chat = await engine.EvaluateAsync(CostControlFeatures.Chat, "Execute", CancellationToken.None);

        Assert.True(chat.Allowed);
        Assert.NotNull(chat.UserNotice);
    }

    private static ThrottleEngine Build(BudgetState state, bool hardStop = false)
    {
        var options = Options.Create(new CostControlOptions
        {
            HardStopEnabled = hardStop,
            BackgroundThrottleDelaySeconds = 30
        });

        return new ThrottleEngine(
            new FakeLedger(state),
            new NullEventBridge(),
            new BudgetWarningThrottler(),
            options,
            NullLogger<ThrottleEngine>.Instance);
    }

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

    private sealed class NullEventBridge : IWorkflowEventBridge
    {
        public bool HasActiveClients => false;
        public void RegisterClient(string clientId) { }
        public void UnregisterClient(string clientId) { }
        public Task PublishEventAsync(WorkflowEvent evt, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public IAsyncEnumerable<WorkflowEvent> ReadEventsAsync(string clientId, CancellationToken cancellationToken) =>
            AsyncEnumerable.Empty<WorkflowEvent>();
    }

    private static class AsyncEnumerable
    {
        public static async IAsyncEnumerable<T> Empty<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
