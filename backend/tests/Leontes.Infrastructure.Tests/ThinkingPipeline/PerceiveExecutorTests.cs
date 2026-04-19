using Leontes.Application.Vision;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Domain.Vision;
using Leontes.Infrastructure.AI.ThinkingPipeline.Executors;
using Leontes.Infrastructure.Vision;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class PerceiveExecutorTests
{
    [Fact]
    public async Task HandleAsync_ClassifiesIntentAndExtractsEntities()
    {
        var ct = TestContext.Current.CancellationToken;
        var executor = CreateExecutor(new VisionOptions { Enabled = false });
        var context = CreateContext("What is the \"quarterly report\"?");
        var workflowContext = new FakeWorkflowContext();

        var result = await executor.HandleAsync(context, workflowContext, ct);

        Assert.Equal("question", result.Intent);
        Assert.Contains("quarterly report", result.ExtractedEntities);
        Assert.Equal(MessageUrgency.Normal, result.Urgency);
    }

    [Fact]
    public async Task HandleAsync_DetectsUrgency()
    {
        var ct = TestContext.Current.CancellationToken;
        var executor = CreateExecutor(new VisionOptions { Enabled = false });
        var context = CreateContext("This is urgent! Fix the bug now.");
        var workflowContext = new FakeWorkflowContext();

        var result = await executor.HandleAsync(context, workflowContext, ct);

        Assert.Equal(MessageUrgency.Critical, result.Urgency);
    }

    [Fact]
    public async Task HandleAsync_EmitsProgressEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        var executor = CreateExecutor(new VisionOptions { Enabled = false });
        var context = CreateContext("Hello");
        var workflowContext = new FakeWorkflowContext();

        await executor.HandleAsync(context, workflowContext, ct);

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
        var executor = CreateExecutor(new VisionOptions { Enabled = false });

        var result = await executor.HandleAsync(context, workflowContext, ct);

        Assert.Equal(msgId, result.MessageId);
        Assert.Equal(convId, result.ConversationId);
        Assert.Equal("test", result.UserContent);
        Assert.Equal("Signal", result.Channel);
    }

    [Fact]
    public async Task HandleAsync_WhenVisionDisabled_SkipsCapture()
    {
        var ct = TestContext.Current.CancellationToken;
        var walker = new RecordingTreeWalker(BuildSampleTree());
        var executor = CreateExecutor(new VisionOptions { Enabled = false }, walker);
        var context = CreateContext("what error is on my screen right now?");

        var result = await executor.HandleAsync(context, new FakeWorkflowContext(), ct);

        Assert.Null(result.ScreenState);
        Assert.Equal(0, walker.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenVisionEnabledAndUserAsksAboutScreen_CapturesState()
    {
        var ct = TestContext.Current.CancellationToken;
        var walker = new RecordingTreeWalker(BuildSampleTree());
        var executor = CreateExecutor(
            new VisionOptions { Enabled = true, RequireExplicitRequest = true },
            walker);
        var context = CreateContext("what error is on my screen?");

        var result = await executor.HandleAsync(context, new FakeWorkflowContext(), ct);

        Assert.NotNull(result.ScreenState);
        Assert.Contains("Window", result.ScreenState);
        Assert.Equal(1, walker.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenExplicitRequestRequiredAndUserDoesNotAsk_SkipsCapture()
    {
        var ct = TestContext.Current.CancellationToken;
        var walker = new RecordingTreeWalker(BuildSampleTree());
        var executor = CreateExecutor(
            new VisionOptions { Enabled = true, RequireExplicitRequest = true },
            walker);
        var context = CreateContext("what's the weather like?");

        var result = await executor.HandleAsync(context, new FakeWorkflowContext(), ct);

        Assert.Null(result.ScreenState);
        Assert.Equal(0, walker.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenWalkerReturnsNull_ScreenStateRemainsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var walker = new RecordingTreeWalker(null);
        var executor = CreateExecutor(
            new VisionOptions { Enabled = true, RequireExplicitRequest = false },
            walker);
        var context = CreateContext("what am I looking at?");

        var result = await executor.HandleAsync(context, new FakeWorkflowContext(), ct);

        Assert.Null(result.ScreenState);
    }

    [Fact]
    public async Task HandleAsync_WhenWalkerThrows_SwallowsExceptionAndContinues()
    {
        var ct = TestContext.Current.CancellationToken;
        var walker = new ThrowingTreeWalker();
        var executor = CreateExecutor(
            new VisionOptions { Enabled = true, RequireExplicitRequest = false },
            walker);
        var context = CreateContext("what is on my screen?");

        var result = await executor.HandleAsync(context, new FakeWorkflowContext(), ct);

        Assert.Null(result.ScreenState);
        Assert.Equal("question", result.Intent);
    }

    private static PerceiveExecutor CreateExecutor(
        VisionOptions visionOptions,
        IUITreeWalker? walker = null) =>
        new(
            walker ?? new RecordingTreeWalker(null),
            new CompactMarkdownSerializer(),
            Options.Create(visionOptions),
            NullLogger<PerceiveExecutor>.Instance);

    private static ThinkingContext CreateContext(string content) => new()
    {
        MessageId = Guid.NewGuid(),
        ConversationId = Guid.NewGuid(),
        UserContent = content,
        Channel = "Cli"
    };

    private static UIElement BuildSampleTree() =>
        new(
            ControlType: "ControlType.Window",
            Name: "Sample Window",
            Value: null,
            AutomationId: null,
            IsEnabled: true,
            IsOffscreen: false,
            BoundingRectangle: null,
            Children:
            [
                new UIElement(
                    ControlType: "ControlType.Button",
                    Name: "OK",
                    Value: null,
                    AutomationId: null,
                    IsEnabled: true,
                    IsOffscreen: false,
                    BoundingRectangle: null,
                    Children: [])
            ]);

    private sealed class RecordingTreeWalker(UIElement? tree) : IUITreeWalker
    {
        public int CallCount { get; private set; }

        public Task<UIElement?> CaptureFocusedWindowTreeAsync(
            TreeWalkerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(tree);
        }

        public Task<UIElement?> CaptureWindowTreeAsync(
            int processId,
            TreeWalkerOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(tree);
    }

    private sealed class ThrowingTreeWalker : IUITreeWalker
    {
        public Task<UIElement?> CaptureFocusedWindowTreeAsync(
            TreeWalkerOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("UIA unavailable");

        public Task<UIElement?> CaptureWindowTreeAsync(
            int processId,
            TreeWalkerOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("UIA unavailable");
    }
}
