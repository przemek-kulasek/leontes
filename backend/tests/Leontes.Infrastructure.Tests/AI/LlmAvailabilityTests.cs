using Leontes.Application.Configuration;
using Leontes.Infrastructure.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.AI;

public sealed class LlmAvailabilityTests
{
    private static LlmAvailability Create(int threshold = 3, int windowMinutes = 5) =>
        new(Options.Create(new ResilienceOptions
        {
            Llm = new LlmResilienceOptions
            {
                ConsecutiveFailuresBeforeDegraded = threshold,
                DegradedWindowMinutes = windowMinutes
            }
        }), NullLogger<LlmAvailability>.Instance);

    [Fact]
    public void RecordFailure_BelowThreshold_StaysAvailable()
    {
        var availability = Create(threshold: 3);

        availability.RecordFailure();
        availability.RecordFailure();

        Assert.True(availability.IsAvailable);
        Assert.Equal(2, availability.ConsecutiveFailures);
    }

    [Fact]
    public void RecordFailure_AtThreshold_EntersDegradedMode()
    {
        var availability = Create(threshold: 2);

        availability.RecordFailure();
        availability.RecordFailure();

        Assert.False(availability.IsAvailable);
    }

    [Fact]
    public void RecordSuccess_AfterDegraded_RestoresAvailability()
    {
        var availability = Create(threshold: 1);
        availability.RecordFailure();
        Assert.False(availability.IsAvailable);

        availability.RecordSuccess();

        Assert.True(availability.IsAvailable);
        Assert.Equal(0, availability.ConsecutiveFailures);
    }
}
