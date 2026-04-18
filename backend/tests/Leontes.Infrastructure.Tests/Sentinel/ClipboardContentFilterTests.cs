using Leontes.Application.Sentinel;
using Leontes.Infrastructure.Sentinel;

namespace Leontes.Infrastructure.Tests.Sentinel;

public sealed class ClipboardContentFilterTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMeta =
        new Dictionary<string, string>();

    private readonly ClipboardContentFilter _filter = new();

    [Fact]
    public void Evaluate_EmptyInput_ReturnsNull()
    {
        var result = _filter.Evaluate("   ", EmptyMeta);

        Assert.Null(result);
    }

    [Fact]
    public void Evaluate_Iban_ClassifiesAsBankAccount()
    {
        var result = _filter.Evaluate("DE89370400440532013000", EmptyMeta);

        Assert.NotNull(result);
        Assert.Equal("iban", result!.Pattern);
        Assert.Equal(SentinelPriority.Medium, result.Priority);
    }

    [Fact]
    public void Evaluate_Email_ClassifiesAsEmail()
    {
        var result = _filter.Evaluate("alice@example.com", EmptyMeta);

        Assert.NotNull(result);
        Assert.Equal("email", result!.Pattern);
    }

    [Fact]
    public void Evaluate_Url_ClassifiesAsUrl()
    {
        var result = _filter.Evaluate("https://example.com/path", EmptyMeta);

        Assert.NotNull(result);
        Assert.Equal("url", result!.Pattern);
    }

    [Fact]
    public void Evaluate_JsonBlock_ClassifiesAsStructured()
    {
        var result = _filter.Evaluate("{\"key\":\"value\"}", EmptyMeta);

        Assert.NotNull(result);
        Assert.Equal("structured", result!.Pattern);
    }

    [Fact]
    public void Evaluate_HighEntropyString_ClassifiesAsCredential()
    {
        var result = _filter.Evaluate("Tr0ub4dor&3!xYz", EmptyMeta);

        Assert.NotNull(result);
        Assert.Equal("credential", result!.Pattern);
        Assert.Equal(SentinelPriority.High, result.Priority);
    }

    [Fact]
    public void Evaluate_PlainSentence_ReturnsNull()
    {
        var result = _filter.Evaluate("hello world this is a plain sentence", EmptyMeta);

        Assert.Null(result);
    }

    [Fact]
    public void Evaluate_Iban_DoesNotLeakRawContent()
    {
        const string iban = "DE89370400440532013000";

        var result = _filter.Evaluate(iban, EmptyMeta);

        Assert.NotNull(result);
        Assert.DoesNotContain(iban, result!.Summary);
        foreach (var value in result.Metadata.Values)
            Assert.DoesNotContain(iban, value);
    }
}
