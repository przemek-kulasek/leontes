using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Domain.Enums;

namespace Leontes.Application.Tests.ProactiveCommunication;

public class WorkflowEventTests
{
    [Fact]
    public void NotificationEvent_CarriesPayload()
    {
        var evt = new NotificationEvent("Meeting", "In 10 minutes", ProactiveUrgency.Medium);

        var payload = Assert.IsType<NotificationPayload>(evt.Data);
        Assert.Equal("Meeting", payload.Title);
        Assert.Equal("In 10 minutes", payload.Content);
        Assert.Equal(ProactiveUrgency.Medium, payload.Urgency);
    }

    [Fact]
    public void ProgressEvent_CarriesPayload()
    {
        var evt = new ProgressEvent("Enrich", "Searching...", 0.5);

        var payload = Assert.IsType<ProgressPayload>(evt.Data);
        Assert.Equal("Enrich", payload.Stage);
        Assert.Equal("Searching...", payload.Description);
        Assert.Equal(0.5, payload.Progress);
    }

    [Fact]
    public void ProgressEvent_ProgressCanBeNull()
    {
        var evt = new ProgressEvent("Plan", "Starting...", null);

        var payload = Assert.IsType<ProgressPayload>(evt.Data);
        Assert.Null(payload.Progress);
    }

    [Fact]
    public void InsightEvent_CarriesPayload()
    {
        var evt = new InsightEvent("You have a meeting soon", "Calendar");

        var payload = Assert.IsType<InsightPayload>(evt.Data);
        Assert.Equal("You have a meeting soon", payload.Content);
        Assert.Equal("Calendar", payload.Source);
    }
}
