namespace Leontes.Application.Vision;

public sealed record TreeSerializerOptions(
    TreeOutputFormat Format = TreeOutputFormat.CompactMarkdown,
    int MaxTokenEstimate = 4000,
    bool IncludeBounds = false);
