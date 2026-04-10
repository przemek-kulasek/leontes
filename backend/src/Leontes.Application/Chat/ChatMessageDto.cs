namespace Leontes.Application.Chat;

public sealed record ChatMessageDto(Guid Id, string Role, string Content, DateTime CreatedAt);
