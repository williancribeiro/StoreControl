namespace StoreControl.Application.Common.Exceptions;

public sealed class RequestValidationException(
    IReadOnlyDictionary<string, string[]> errors) : Exception("A solicitação contém dados inválidos.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public RequestValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }
}

public sealed class ResourceNotFoundException(string message) : Exception(message);

public sealed class ResourceConflictException(string message) : Exception(message);

public sealed class OperationForbiddenException(string message) : Exception(message);
