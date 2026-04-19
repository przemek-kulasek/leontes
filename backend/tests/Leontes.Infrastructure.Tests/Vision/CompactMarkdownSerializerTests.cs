using Leontes.Application.Vision;
using Leontes.Domain.Vision;
using Leontes.Infrastructure.Vision;

namespace Leontes.Infrastructure.Tests.Vision;

public sealed class CompactMarkdownSerializerTests
{
    private readonly CompactMarkdownSerializer _serializer = new();

    [Fact]
    public void Serialize_WithNamedWindow_OutputsBracketedLabel()
    {
        var root = Element("ControlType.Window", name: "Visual Studio Code");

        var output = _serializer.Serialize(root);

        Assert.Contains("[Window: Visual Studio Code]", output);
    }

    [Fact]
    public void Serialize_StripsControlTypePrefix()
    {
        var root = Element("ControlType.Button", name: "OK");

        var output = _serializer.Serialize(root);

        Assert.DoesNotContain("ControlType.", output);
        Assert.Contains("[Button: OK]", output);
    }

    [Fact]
    public void Serialize_IncludesValueWhenPresent()
    {
        var root = Element("ControlType.Edit", name: "Editor", value: "hello world");

        var output = _serializer.Serialize(root);

        Assert.Contains("[Edit: Editor] text content: \"hello world\"", output);
    }

    [Fact]
    public void Serialize_IndentsChildrenByDepth()
    {
        var child = Element("ControlType.Button", name: "OK");
        var root = Element("ControlType.Window", name: "Dialog", children: [child]);

        var output = _serializer.Serialize(root);

        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("[Window: Dialog]", lines[0]);
        Assert.StartsWith("  [Button: OK]", lines[1]);
    }

    [Fact]
    public void Serialize_DisabledElement_MarksAsDisabled()
    {
        var root = Element("ControlType.Button", name: "Submit", isEnabled: false);

        var output = _serializer.Serialize(root);

        Assert.Contains("(disabled)", output);
    }

    [Fact]
    public void Serialize_WithIncludeBounds_AppendsBoundingRectangle()
    {
        var root = Element(
            "ControlType.Window",
            name: "App",
            bounds: new Rect(10, 20, 300, 400));

        var output = _serializer.Serialize(
            root,
            new TreeSerializerOptions(IncludeBounds: true));

        Assert.Contains("@(10,20,300x400)", output);
    }

    [Fact]
    public void Serialize_WithoutIncludeBounds_OmitsBoundingRectangle()
    {
        var root = Element(
            "ControlType.Window",
            name: "App",
            bounds: new Rect(10, 20, 300, 400));

        var output = _serializer.Serialize(root);

        Assert.DoesNotContain("@(", output);
    }

    [Fact]
    public void Serialize_WhenOutputExceedsBudget_TruncatesDeepestFirst()
    {
        // Build a deep tree: 10 levels, each with 3 children.
        var deepTree = BuildDeepTree(depth: 6, fanout: 4);
        var options = new TreeSerializerOptions(MaxTokenEstimate: 100); // ~400 chars

        var output = _serializer.Serialize(deepTree, options);

        Assert.True(output.Length <= 400,
            $"expected <= 400 chars after truncation, was {output.Length}");
        Assert.Contains("truncated to fit token budget", output);
        Assert.Contains("[Window: Root]", output);
    }

    [Fact]
    public void Serialize_PasswordPlaceholderValueIsPreserved()
    {
        // The walker is responsible for inserting the placeholder — the serializer must not strip it.
        var root = Element(
            "ControlType.Edit",
            name: "Password",
            value: "[password field]");

        var output = _serializer.Serialize(root);

        Assert.Contains("[password field]", output);
    }

    private static UIElement Element(
        string controlType,
        string? name = null,
        string? value = null,
        string? automationId = null,
        bool isEnabled = true,
        bool isOffscreen = false,
        Rect? bounds = null,
        IReadOnlyList<UIElement>? children = null) =>
        new(controlType, name, value, automationId, isEnabled, isOffscreen, bounds, children ?? []);

    private static UIElement BuildDeepTree(int depth, int fanout)
    {
        if (depth == 0)
            return Element("ControlType.Text", name: "leaf");

        var children = new List<UIElement>(fanout);
        for (var i = 0; i < fanout; i++)
            children.Add(BuildDeepTree(depth - 1, fanout));

        return Element("ControlType.Window", name: depth == 6 ? "Root" : $"Level{depth}", children: children);
    }
}
