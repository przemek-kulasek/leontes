using System.Security.Cryptography;

namespace Leontes.Cli.Auth;

public static class ApiKeyGenerator
{
    private const string Prefix = "lnt_";
    private const int KeyLengthBytes = 32;

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(KeyLengthBytes);
        return Prefix + Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
