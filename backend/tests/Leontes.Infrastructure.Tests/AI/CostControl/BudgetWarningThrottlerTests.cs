using Leontes.Domain.Enums;
using Leontes.Infrastructure.AI.CostControl;

namespace Leontes.Infrastructure.Tests.AI.CostControl;

public sealed class BudgetWarningThrottlerTests
{
    [Fact]
    public void ShouldNotify_WhenStateIsNormal_ReturnsFalse()
    {
        var throttler = new BudgetWarningThrottler();

        Assert.False(throttler.ShouldNotify(BudgetState.Normal));
    }

    [Fact]
    public void ShouldNotify_OnFirstNonNormalState_ReturnsTrue()
    {
        var throttler = new BudgetWarningThrottler();

        Assert.True(throttler.ShouldNotify(BudgetState.Warning));
    }

    [Fact]
    public void ShouldNotify_WhenCalledTwiceWithSameState_ReturnsFalseTheSecondTime()
    {
        var throttler = new BudgetWarningThrottler();

        Assert.True(throttler.ShouldNotify(BudgetState.Warning));
        Assert.False(throttler.ShouldNotify(BudgetState.Warning));
    }

    [Fact]
    public void ShouldNotify_WhenStateEscalates_ReturnsTrue()
    {
        var throttler = new BudgetWarningThrottler();

        Assert.True(throttler.ShouldNotify(BudgetState.Warning));
        Assert.True(throttler.ShouldNotify(BudgetState.Throttled));
    }
}
