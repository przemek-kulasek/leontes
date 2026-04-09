namespace Leontes.Domain.Exceptions;

public sealed class NotFoundException(string message) : DomainException(message);
