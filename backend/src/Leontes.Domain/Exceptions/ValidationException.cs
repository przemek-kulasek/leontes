namespace Leontes.Domain.Exceptions;

public sealed class ValidationException(string message) : DomainException(message);
