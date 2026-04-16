using Leontes.Domain.Entities;
using Leontes.Domain.Enums;

namespace Leontes.Domain.Tests.Entities;

public class StoredProactiveEventTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_CreatesEvent()
    {
        var evt = new StoredProactiveEvent
        {
            EventType = "Notification",
            PayloadJson = """{"title":"Test"}"""
        };

        Assert.Equal("Notification", evt.EventType);
        Assert.Equal("""{"title":"Test"}""", evt.PayloadJson);
    }

    [Fact]
    public void Status_DefaultsToDefault()
    {
        var evt = new StoredProactiveEvent
        {
            EventType = "Notification",
            PayloadJson = "{}"
        };

        Assert.Equal(ProactiveEventStatus.Pending, evt.Status);
    }

    [Fact]
    public void Urgency_DefaultsToDefault()
    {
        var evt = new StoredProactiveEvent
        {
            EventType = "Notification",
            PayloadJson = "{}"
        };

        Assert.Equal(ProactiveUrgency.Low, evt.Urgency);
    }

    [Fact]
    public void OptionalFields_AreNull()
    {
        var evt = new StoredProactiveEvent
        {
            EventType = "Progress",
            PayloadJson = "{}"
        };

        Assert.Null(evt.RequestId);
        Assert.Null(evt.Response);
        Assert.Null(evt.DeliveredAt);
        Assert.Null(evt.RespondedAt);
    }
}
