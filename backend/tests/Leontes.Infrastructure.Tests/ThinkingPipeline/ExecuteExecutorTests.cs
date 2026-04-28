using System.Runtime.CompilerServices;
using Leontes.Application.Configuration;
using Leontes.Application.CostControl;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Executors;
using Leontes.Infrastructure.AI.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class ExecuteExecutorTests
{
    [Fact]
    public async Task HandleAsync_MessageAlreadyComplete_SkipsLlmCall()
    {
        var ct = TestContext.Current.CancellationToken;
        var chatClient = new RecordingChatClient();
        var executor = CreateExecutor(chatClient, tools: []);
        var context = CreateContext();
        context.Response = "Pre-finalized response from upstream stage.";
        context.IsComplete = true;

        var result = await executor.HandleAsync(context, new FakeWorkflowContext(), ct);

        Assert.Equal("Pre-finalized response from upstream stage.", result.Response);
        Assert.True(result.IsComplete);
        Assert.Equal(0, chatClient.StreamingCalls);
    }

    [Fact]
    public async Task HandleAsync_NoSelectedTools_PassesAllToolsToChatOptions()
    {
        var ct = TestContext.Current.CancellationToken;
        var chatClient = new RecordingChatClient();
        var dateTool = AIFunctionFactory.Create(CurrentDateTimeTool.GetCurrentDateTime);
        var executor = CreateExecutor(chatClient, tools: [dateTool]);
        var context = CreateContext();
        context.SelectedTools = [];

        await executor.HandleAsync(context, new FakeWorkflowContext(), ct);

        Assert.Equal(1, chatClient.StreamingCalls);
        Assert.NotNull(chatClient.LastOptions);
        Assert.Single(chatClient.LastOptions!.Tools!);
    }

    [Fact]
    public async Task HandleAsync_SelectedToolsSubset_FiltersToolsByName()
    {
        var ct = TestContext.Current.CancellationToken;
        var chatClient = new RecordingChatClient();
        var dateTool = AIFunctionFactory.Create(CurrentDateTimeTool.GetCurrentDateTime);
        var unusedTool = AIFunctionFactory.Create(() => "unused", name: "OtherTool");
        var executor = CreateExecutor(chatClient, tools: [dateTool, unusedTool]);
        var context = CreateContext();
        context.SelectedTools = [dateTool.Name];

        await executor.HandleAsync(context, new FakeWorkflowContext(), ct);

        Assert.NotNull(chatClient.LastOptions);
        Assert.Single(chatClient.LastOptions!.Tools!);
        Assert.Equal(dateTool.Name, ((AIFunction)chatClient.LastOptions.Tools![0]).Name);
    }

    private static ExecuteExecutor CreateExecutor(IChatClient chatClient, IEnumerable<AITool> tools) =>
        new(
            chatClient,
            new PersonaInstructions("You are Leontes."),
            tools,
            new NoopTokenMeter(),
            Options.Create(new PersonaOptions()),
            Options.Create(new AiProviderOptions()),
            NullLogger<ExecuteExecutor>.Instance);

    private static ThinkingContext CreateContext() => new()
    {
        MessageId = Guid.NewGuid(),
        ConversationId = Guid.NewGuid(),
        UserContent = "What time is it?",
        Channel = "Cli"
    };

    private sealed class NoopTokenMeter : ITokenMeter
    {
        public void Record(string feature, string operation, string modelId, int input, int output) { }
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public int StreamingCalls { get; private set; }
        public ChatOptions? LastOptions { get; private set; }

        public void Dispose() { }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamingCalls++;
            LastOptions = options;
            yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextContent("ok")]);
            await Task.CompletedTask;
        }
    }
}
