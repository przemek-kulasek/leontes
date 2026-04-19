namespace Leontes.Application.Vision;

public sealed record TreeWalkerOptions(
    int MaxDepth = 8,
    bool IncludeOffscreen = false,
    bool IncludeValues = true,
    IReadOnlyList<string>? ExcludeControlTypes = null);
