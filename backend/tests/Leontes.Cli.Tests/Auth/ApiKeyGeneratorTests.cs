using Leontes.Cli.Auth;

namespace Leontes.Cli.Tests.Auth;

public sealed class ApiKeyGeneratorTests
{
    [Fact]
    public void Generate_ReturnsKeyWithLntPrefix()
    {
        var key = ApiKeyGenerator.Generate();

        Assert.StartsWith("lnt_", key);
    }

    [Fact]
    public void Generate_ReturnsKeyOfExpectedLength()
    {
        var key = ApiKeyGenerator.Generate();

        // lnt_ prefix (4 chars) + 32 bytes Base64URL-encoded (43 chars without padding)
        Assert.Equal(47, key.Length);
    }

    [Fact]
    public void Generate_ReturnsDifferentKeysOnEachCall()
    {
        var key1 = ApiKeyGenerator.Generate();
        var key2 = ApiKeyGenerator.Generate();

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Generate_ReturnsUrlSafeCharactersOnly()
    {
        var key = ApiKeyGenerator.Generate();
        var keyWithoutPrefix = key["lnt_".Length..];

        Assert.DoesNotContain("+", keyWithoutPrefix);
        Assert.DoesNotContain("/", keyWithoutPrefix);
        Assert.DoesNotContain("=", keyWithoutPrefix);
    }
}
