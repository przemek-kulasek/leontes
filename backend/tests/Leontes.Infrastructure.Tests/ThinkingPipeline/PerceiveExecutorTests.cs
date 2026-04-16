using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Executors;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class PerceiveExecutorTests
{
    private readonly PerceiveExecutor _executor = new(NullLogger<PerceiveExecutor>.Instance);

    [Fact]
    public async Task HandleAsync_ClassifiesIntentAndExtractsEntities()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext("What is the \"quarterly report\"?");
        var workflowContext = new FakeWorkflowContext();

        var result = await _executor.HandleAsync(context, workflowContext, ct);

        Assert.Equal("question", result.Intent);
        Assert.Contains("quarterly report", result.ExtractedEntities);
        Assert.Equal(MessageUrgency.Normal, result.Urgency);
    }

    [Fact]
    public async Task HandleAsync_DetectsUrgency()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext("This is urgent! Fix the bug now.");
        var workflowContext = new FakeWorkflowContext();

        var result = await _executor.HandleAsync(context, workflowContext, ct);

        Assert.Equal(MessageUrgency.Critical, result.Urgency);
    }

    [Fact]
    public async Task HandleAsync_EmitsProgressEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext("Hello");
        var workflowContext = new FakeWorkflowContext();

        await _executor.HandleAsync(context, workflowContext, ct);

        Assert.True(workflowContext.Events.Count >= 2);
    }

    [Fact]
    public async Task HandleAsync_PreservesOriginalContext()
    {
        var ct = TestContext.Current.CancellationToken;
        var msgId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var context = new ThinkingContext
        {
            MessageId = msgId,
            ConversationId = convId,
            UserContent = "test",
            Channel = "Signal"
        };
        var workflowContext = new FakeWorkflowContext();

        var result = await _executor.HandleAsync(context, workflowContext, ct);

        Assert.Equal(msgId, result.MessageId);
        Assert.Equal(convId, result.ConversationId);
        Assert.Equal("test", result.UserContent);
        Assert.Equal("Signal", result.Channel);
    }

    private static ThinkingContext CreateContext(string content) => new()
    {
        MessageId = Guid.NewGuid(),
        ConversationId = Guid.NewGuid(),
        UserContent = content,
        Channel = "Cli"
    };
}
