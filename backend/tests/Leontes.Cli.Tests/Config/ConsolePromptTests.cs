using Leontes.Cli.Config;

namespace Leontes.Cli.Tests.Config;

public sealed class ConsolePromptTests
{
    [Fact]
    public void AskWithDefault_EmptyInput_ReturnsDefault()
    {
        using var reader = new StringReader("\n");
        using var writer = new StringWriter();

        var result = ConsolePrompt.AskWithDefault(reader, writer, "Model ID", "qwen2.5:7b");

        Assert.Equal("qwen2.5:7b", result);
    }

    [Fact]
    public void AskWithDefault_WhitespaceInput_ReturnsDefault()
    {
        using var reader = new StringReader("   \n");
        using var writer = new StringWriter();

        var result = ConsolePrompt.AskWithDefault(reader, writer, "Model ID", "qwen2.5:7b");

        Assert.Equal("qwen2.5:7b", result);
    }

    [Fact]
    public void AskWithDefault_UserProvidesValue_ReturnsTrimmedValue()
    {
        using var reader = new StringReader("  llama3:8b  \n");
        using var writer = new StringWriter();

        var result = ConsolePrompt.AskWithDefault(reader, writer, "Model ID", "qwen2.5:7b");

        Assert.Equal("llama3:8b", result);
    }

    [Fact]
    public void AskWithDefault_WritesLabelAndDefaultToPrompt()
    {
        using var reader = new StringReader("\n");
        using var writer = new StringWriter();

        ConsolePrompt.AskWithDefault(reader, writer, "Model ID", "qwen2.5:7b");

        var output = writer.ToString();
        Assert.Contains("Model ID", output);
        Assert.Contains("qwen2.5:7b", output);
    }

    [Fact]
    public void AskWithDefault_NullInput_ReturnsDefault()
    {
        using var reader = new StringReader(string.Empty);
        using var writer = new StringWriter();

        var result = ConsolePrompt.AskWithDefault(reader, writer, "Model ID", "qwen2.5:7b");

        Assert.Equal("qwen2.5:7b", result);
    }

    [Fact]
    public void AskWithDefault_FlushesPromptBeforeReading()
    {
        using var reader = new FlushTrackingReader("value\n");
        using var writer = new FlushTrackingWriter(reader);

        ConsolePrompt.AskWithDefault(reader, writer, "Label", "default");

        Assert.True(writer.FlushedBeforeRead, "writer must flush before ReadLine");
    }

    private sealed class FlushTrackingReader(string content) : StringReader(content)
    {
        public bool HasRead { get; private set; }

        public override string? ReadLine()
        {
            HasRead = true;
            return base.ReadLine();
        }
    }

    private sealed class FlushTrackingWriter(FlushTrackingReader reader) : StringWriter
    {
        public bool FlushedBeforeRead { get; private set; }

        public override void Flush()
        {
            if (!reader.HasRead)
                FlushedBeforeRead = true;
            base.Flush();
        }
    }
}
