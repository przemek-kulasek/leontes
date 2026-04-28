using Leontes.Infrastructure.AI.Tools;

namespace Leontes.Infrastructure.Tests.AI.Tools;

public sealed class ListFilesToolTests : IDisposable
{
    private readonly string _tempDir;

    public ListFilesToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "leontes-listfiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ListFiles_EmptyPath_ReturnsError()
    {
        var result = ListFilesTool.ListFiles("");

        Assert.StartsWith("Error", result);
    }

    [Fact]
    public void ListFiles_NonexistentPath_ReturnsError()
    {
        var result = ListFilesTool.ListFiles(Path.Combine(_tempDir, "no-such-dir"));

        Assert.Contains("not found", result);
    }

    [Fact]
    public void ListFiles_EmptyDirectory_ReturnsNoMatchMessage()
    {
        var result = ListFilesTool.ListFiles(_tempDir);

        Assert.Contains("No entries", result);
    }

    [Fact]
    public void ListFiles_FilteredByPattern_ReturnsOnlyMatches()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "");
        File.WriteAllText(Path.Combine(_tempDir, "b.md"), "");
        File.WriteAllText(Path.Combine(_tempDir, "c.txt"), "");

        var result = ListFilesTool.ListFiles(_tempDir, "*.txt");

        Assert.Contains("a.txt", result);
        Assert.Contains("c.txt", result);
        Assert.DoesNotContain("b.md", result);
    }

    [Fact]
    public void ListFiles_OverLimit_ReportsTruncation()
    {
        for (int i = 0; i < 105; i++)
            File.WriteAllText(Path.Combine(_tempDir, $"file{i:D3}.txt"), "");

        var result = ListFilesTool.ListFiles(_tempDir);

        Assert.Contains("[truncated", result);
    }
}
