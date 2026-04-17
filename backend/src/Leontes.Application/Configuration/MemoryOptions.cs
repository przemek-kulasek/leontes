namespace Leontes.Application.Configuration;

public sealed class MemoryOptions
{
    public const string SectionName = "Memory";

    /// <summary>
    /// Fixed at the schema level — changing this requires a migration and re-embedding all stored content.
    /// Matches the output dimension of the default embedding model (all-MiniLM-L6-v2).
    /// </summary>
    public const int EmbeddingDimensions = 384;

    public int ConsolidationIntervalHours { get; set; } = 1;
    public int MaxRetrievalResults { get; set; } = 5;
    public double MinRelevanceThreshold { get; set; } = 0.7;
    public string EmbeddingModelId { get; set; } = "all-minilm:l6-v2";
    public string? EmbeddingEndpoint { get; set; }
    public string EmbeddingProvider { get; set; } = "ollama";
}
