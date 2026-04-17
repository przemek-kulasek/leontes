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
    public void AskWithDefault_ParameterlessOverload_UsesConsoleStreams()
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;

        try
        {
            using var reader = new StringReader("\n");
            using var writer = new StringWriter();
            Console.SetIn(reader);
            Console.SetOut(writer);

            var result = ConsolePrompt.AskWithDefault("Model ID", "qwen2.5:7b");

            Assert.Equal("qwen2.5:7b", result);
            Assert.Contains("Model ID", writer.ToString());
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }
}
