namespace Leontes.Application.CostControl;

public sealed record TokenUsage(
    string Feature,
    string Operation,
    string ModelId,
    int InputTokens,
    int OutputTokens,
    DateTime Timestamp);
