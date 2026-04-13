using Microsoft.Extensions.Configuration;

namespace Leontes.Cli.Config;

public sealed class CliConfiguration
{
    private readonly IConfiguration _configuration;

    public CliConfiguration()
    {
        _configuration = new ConfigurationBuilder()
            .AddUserSecrets<CliConfiguration>()
            .Build();
    }

    public string? ApiKey => _configuration["Authentication:ApiKey"];

    public string BaseUrl => _configuration["Api:BaseUrl"] ?? "http://localhost:5154";
}
