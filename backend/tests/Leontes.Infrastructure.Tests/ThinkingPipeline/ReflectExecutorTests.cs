using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Executors;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class ReflectExecutorTests
{
    private readonly ReflectExecutor _executor = new(
        new NullMemoryStore(),
        new NullSynapseGraph(),
        new NullDecisionRecorder(),
        NullLogger<ReflectExecutor>.Instance);

    [Fact]
    public async Task HandleAsync_IncompleteResponse_SkipsReflection()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext();
        context.Response = "partial";
        context.IsComplete = false;
        var workflowContext = new FakeWorkflowContext();

        var result = await _executor.HandleAsync(context, workflowContext, ct);

        Assert.False(result.IsComplete);
        Assert.Empty(result.NewInsights);
    }

    [Fact]
    public async Task HandleAsync_CompleteResponse_ExtractsInsights()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext();
        context.Response = "Here are your files.";
        context.IsComplete = true;
        context.Intent = "search";
        context.ToolResults =
        [
            new ToolCallResult("file_search", "*.pdf", "Found 3 files", true)
        ];
        var workflowContext = new FakeWorkflowContext();

        var result = await _executor.HandleAsync(context, workflowContext, ct);

        Assert.NotEmpty(result.NewInsights);
    }

    [Fact]
    public async Task HandleAsync_CompleteWithInsights_EmitsInsightEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext();
        context.Response = "Done.";
        context.IsComplete = true;
        context.ResolvedEntities =
        [
            new ResolvedEntity("john", Guid.NewGuid(), "Person", "John Smith")
        ];
        var workflowContext = new FakeWorkflowContext();

        await _executor.HandleAsync(context, workflowContext, ct);

        Assert.Contains(workflowContext.Events, e => e is InsightEvent);
    }

    [Fact]
    public async Task HandleAsync_CompleteWithNoInsights_DoesNotEmitInsightEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext();
        context.Response = "Hello!";
        context.IsComplete = true;
        var workflowContext = new FakeWorkflowContext();

        await _executor.HandleAsync(context, workflowContext, ct);

        Assert.DoesNotContain(workflowContext.Events, e => e is InsightEvent);
    }

    [Fact]
    public async Task HandleAsync_EmitsProgressEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext();
        context.Response = "Done.";
        context.IsComplete = true;
        var workflowContext = new FakeWorkflowContext();

        await _executor.HandleAsync(context, workflowContext, ct);

        Assert.Contains(workflowContext.Events, e => e is ProgressEvent);
    }

    private static ThinkingContext CreateContext() => new()
    {
        MessageId = Guid.NewGuid(),
        ConversationId = Guid.NewGuid(),
        UserContent = "test message",
        Channel = "Cli"
    };
}
