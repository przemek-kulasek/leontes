using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Executors;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class PlanResumeExecutorTests
{
    private readonly PlanResumeExecutor _executor = new(NullLogger<PlanResumeExecutor>.Instance);

    [Fact]
    public async Task HandleAsync_WithSavedState_RestoresContextAndSetsResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var originalContext = new ThinkingContext
        {
            MessageId = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            UserContent = "Do something ambiguous",
            Channel = "Cli",
            RequiresHumanInput = true,
            HumanInputQuestion = "What exactly?"
        };

        var workflowContext = new FakeWorkflowContext();
        await workflowContext.QueueStateUpdateAsync("PlanThinkingContext", originalContext, "shared", ct);

        var result = await _executor.HandleAsync("Use option A", workflowContext, ct);

        Assert.Equal("Use option A", result.HumanInputResponse);
        Assert.False(result.RequiresHumanInput);
        Assert.Equal(originalContext.MessageId, result.MessageId);
        Assert.Equal(originalContext.ConversationId, result.ConversationId);
    }

    [Fact]
    public async Task HandleAsync_WithNoSavedState_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var workflowContext = new FakeWorkflowContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _executor.HandleAsync("response", workflowContext, ct).AsTask());
    }

    [Fact]
    public async Task HandleAsync_SetsDefaultPlanWhenNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var originalContext = new ThinkingContext
        {
            MessageId = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            UserContent = "Something",
            Channel = "Signal"
        };

        var workflowContext = new FakeWorkflowContext();
        await workflowContext.QueueStateUpdateAsync("PlanThinkingContext", originalContext, "shared", ct);

        var result = await _executor.HandleAsync("clarification text", workflowContext, ct);

        Assert.NotNull(result.Plan);
        Assert.Contains("clarification text", result.Plan);
    }

    [Fact]
    public async Task HandleAsync_EmitsProgressEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var originalContext = new ThinkingContext
        {
            MessageId = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            UserContent = "test",
            Channel = "Cli"
        };

        var workflowContext = new FakeWorkflowContext();
        await workflowContext.QueueStateUpdateAsync("PlanThinkingContext", originalContext, "shared", ct);

        await _executor.HandleAsync("answer", workflowContext, ct);

        Assert.NotEmpty(workflowContext.Events);
    }
}
