using System.Runtime.CompilerServices;
using Leontes.Application.Configuration;
using Leontes.Application.CostControl;
using Leontes.Application.ThinkingPipeline;
using Leontes.Application;
using Leontes.Domain.Entities;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Executors;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class PlanExecutorThrottleTests
{
    [Fact]
    public async Task HandleAsync_BudgetThrottleDenied_ShortCircuitsWithoutCallingLlm()
    {
        var ct = TestContext.Current.CancellationToken;
        var chatClient = new RecordingChatClient();
        var executor = CreateExecutor(
            chatClient,
            new FakeThrottleEngine(new ThrottleDecision(false, null, "Budget exhausted.", null)));
        var context = CreateContext();
        var workflowContext = new FakeWorkflowContext();

        await executor.HandleAsync(context, workflowContext, ct);

        Assert.Equal(0, chatClient.Calls);
        Assert.True(context.IsComplete);
        Assert.Contains("Budget exhausted.", context.Response);
        Assert.Empty(context.SelectedTools);
        Assert.Contains(workflowContext.SentMessages, m => m is ThinkingContext ctx && ctx.IsComplete);
    }

    [Fact]
    public async Task HandleAsync_BudgetThrottleAllowed_CallsLlmNormally()
    {
        var ct = TestContext.Current.CancellationToken;
        var chatClient = new RecordingChatClient();
        var executor = CreateExecutor(
            chatClient,
            new FakeThrottleEngine(new ThrottleDecision(true, null, null, null)));
        var context = CreateContext();
        var workflowContext = new FakeWorkflowContext();

        await executor.HandleAsync(context, workflowContext, ct);

        Assert.Equal(1, chatClient.Calls);
        Assert.False(context.IsComplete);
    }

    private static PlanExecutor CreateExecutor(IChatClient chatClient, IThrottleEngine throttleEngine) =>
        new(
            chatClient,
            new PersonaInstructions("You are Leontes."),
            new NoopTokenMeter(),
            throttleEngine,
            new NoopDecisionRecorder(),
            Options.Create(new PersonaOptions()),
            Options.Create(new AiProviderOptions()),
            NullLogger<PlanExecutor>.Instance);

    private static ThinkingContext CreateContext() => new()
    {
        MessageId = Guid.NewGuid(),
        ConversationId = Guid.NewGuid(),
        UserContent = "Hi",
        Channel = "Cli"
    };

    private sealed class FakeThrottleEngine(ThrottleDecision decision) : IThrottleEngine
    {
        public Task<ThrottleDecision> EvaluateAsync(string feature, string operation, CancellationToken cancellationToken) =>
            Task.FromResult(decision);
    }

    private sealed class NoopTokenMeter : ITokenMeter
    {
        public void Record(string feature, string operation, string modelId, int input, int output) { }
    }

    private sealed class NoopDecisionRecorder : IDecisionRecorder
    {
        public void Record(
            Guid requestId, string stageName, string decisionType,
            string chosen, string rationale,
            IReadOnlyList<DecisionCandidate>? candidates = null)
        { }
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public int Calls { get; private set; }

        public void Dispose() { }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Calls++;
            yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextContent("ok")]);
            await Task.CompletedTask;
        }
    }
}
