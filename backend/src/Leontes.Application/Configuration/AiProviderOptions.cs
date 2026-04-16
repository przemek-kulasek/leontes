namespace Leontes.Application.Configuration;

public sealed class AiProviderOptions
{
    public const string SectionName = "AiProvider";

    public Dictionary<string, ModelOptions> Models { get; set; } = new();
}

public sealed class ModelOptions
{
    public string Provider { get; set; } = "ollama";
    public string ModelId { get; set; } = "qwen2.5:7b";
    public string? Endpoint { get; set; }
}
