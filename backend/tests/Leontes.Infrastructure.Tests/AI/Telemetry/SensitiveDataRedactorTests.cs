using Leontes.Application.Configuration;
using Leontes.Infrastructure.AI.Telemetry;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.AI.Telemetry;

public sealed class SensitiveDataRedactorTests
{
    [Fact]
    public void Redact_WithMatchingPattern_ReturnsPlaceholder()
    {
        var redactor = Create("password", "token");

        var result = redactor.Redact("the user's password is hunter2");

        Assert.Equal("[REDACTED]", result);
    }

    [Fact]
    public void Redact_CaseInsensitive_ReturnsPlaceholder()
    {
        var redactor = Create("secret");

        var result = redactor.Redact("A SECRET value");

        Assert.Equal("[REDACTED]", result);
    }

    [Fact]
    public void Redact_WithoutMatch_ReturnsInput()
    {
        var redactor = Create("password");

        var result = redactor.Redact("a benign sentence");

        Assert.Equal("a benign sentence", result);
    }

    [Fact]
    public void Redact_NullOrEmpty_ReturnsEmpty()
    {
        var redactor = Create("password");

        Assert.Equal(string.Empty, redactor.Redact(null));
        Assert.Equal(string.Empty, redactor.Redact(""));
    }

    [Fact]
    public void Redact_WithNoPatternsConfigured_ReturnsInputUnchanged()
    {
        var redactor = Create();

        var result = redactor.Redact("anything including password");

        Assert.Equal("anything including password", result);
    }

    private static SensitiveDataRedactor Create(params string[] patterns) =>
        new(Options.Create(new TelemetryOptions { SensitiveFieldPatterns = patterns }));
}
