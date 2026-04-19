namespace Leontes.Application.Configuration;

public sealed class MemoryOptions
{
    public const string SectionName = "Memory";

    /// <summary>
    /// Fixed at the schema level — changing this requires a migration and re-embedding all stored content.
    /// Matches the output dimension of the default embedding model (nomic-embed-text).
    /// </summary>
    public const int EmbeddingDimensions = 768;

    public int ConsolidationIntervalHours { get; set; } = 1;
    public string EmbeddingModelId { get; set; } = "nomic-embed-text";
    public string? EmbeddingEndpoint { get; set; }
    public string EmbeddingProvider { get; set; } = "ollama";
}
