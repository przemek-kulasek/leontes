namespace Leontes.Application.ProactiveCommunication.Requests;

public sealed record QuestionRequest(
    string Title,
    string Content,
    IReadOnlyList<string>? Options,
    TimeSpan? Timeout);
