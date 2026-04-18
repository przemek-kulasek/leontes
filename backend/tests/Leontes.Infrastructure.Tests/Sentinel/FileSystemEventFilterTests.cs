using Leontes.Application.Sentinel;
using Leontes.Infrastructure.Sentinel;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.Sentinel;

public sealed class FileSystemEventFilterTests
{
    private readonly FileSystemEventFilter _filter = new(Options.Create(new SentinelOptions()));

    [Fact]
    public void Evaluate_SensitiveExtension_ReturnsHighPriority()
    {
        var meta = Meta(changeType: "Created");

        var result = _filter.Evaluate("C:/work/.env", meta);

        Assert.NotNull(result);
        Assert.Equal("sensitive-file", result!.Pattern);
        Assert.Equal(SentinelPriority.High, result.Priority);
    }

    [Fact]
    public void Evaluate_LargeFileCreated_ReturnsLargeFile()
    {
        var meta = Meta(changeType: "Created", size: 200L * 1024 * 1024);

        var result = _filter.Evaluate("C:/Downloads/big.iso", meta);

        Assert.NotNull(result);
        Assert.Equal("large-file", result!.Pattern);
    }

    [Fact]
    public void Evaluate_InvoiceFile_ReturnsInvoice()
    {
        var meta = Meta(changeType: "Created");

        var result = _filter.Evaluate("C:/Downloads/invoice-2024.pdf", meta);

        Assert.NotNull(result);
        Assert.Equal("invoice", result!.Pattern);
    }

    [Fact]
    public void Evaluate_RapidChanges_ReturnsRapidChanges()
    {
        var meta = Meta(changeType: "Changed", recent: 75);

        var result = _filter.Evaluate("C:/project/src/app.cs", meta);

        Assert.NotNull(result);
        Assert.Equal("rapid-changes", result!.Pattern);
    }

    [Fact]
    public void Evaluate_UnremarkableFile_ReturnsNull()
    {
        var meta = Meta(changeType: "Changed", size: 1024);

        var result = _filter.Evaluate("C:/work/notes.md", meta);

        Assert.Null(result);
    }

    private static IReadOnlyDictionary<string, string> Meta(
        string changeType,
        long? size = null,
        int? recent = null)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [FileSystemEventFilter.MetaChangeType] = changeType
        };
        if (size.HasValue)
            dict[FileSystemEventFilter.MetaSizeBytes] = size.Value.ToString();
        if (recent.HasValue)
            dict[FileSystemEventFilter.MetaRecentEventCount] = recent.Value.ToString();
        return dict;
    }
}
