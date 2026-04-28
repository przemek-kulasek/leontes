using Leontes.Infrastructure.AI.Tools;

namespace Leontes.Infrastructure.Tests.AI.Tools;

public sealed class ReadFileToolTests : IDisposable
{
    private readonly string _tempDir;

    public ReadFileToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "leontes-readfile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ReadFile_EmptyPath_ReturnsError()
    {
        var result = ReadFileTool.ReadFile("");

        Assert.StartsWith("Error", result);
    }

    [Fact]
    public void ReadFile_NonexistentFile_ReturnsError()
    {
        var result = ReadFileTool.ReadFile(Path.Combine(_tempDir, "missing.txt"));

        Assert.Contains("not found", result);
    }

    [Fact]
    public void ReadFile_EmptyFile_ReturnsEmptyMarker()
    {
        var path = Path.Combine(_tempDir, "empty.txt");
        File.WriteAllText(path, "");

        var result = ReadFileTool.ReadFile(path);

        Assert.Equal("(empty file)", result);
    }

    [Fact]
    public void ReadFile_SmallFile_ReturnsContents()
    {
        var path = Path.Combine(_tempDir, "small.txt");
        File.WriteAllText(path, "hello world");

        var result = ReadFileTool.ReadFile(path);

        Assert.Equal("hello world", result);
    }

    [Fact]
    public void ReadFile_LargeFile_TruncatesAndReports()
    {
        var path = Path.Combine(_tempDir, "large.txt");
        var content = new string('a', 70 * 1024);
        File.WriteAllText(path, content);

        var result = ReadFileTool.ReadFile(path);

        Assert.Contains("[truncated", result);
    }
}
