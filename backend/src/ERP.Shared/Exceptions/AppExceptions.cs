namespace ERP.Shared.Exceptions;

public abstract class AppException : Exception
{
    public abstract string Code { get; }
    protected AppException(string message) : base(message) { }
}

public sealed class NotFoundException : AppException
{
    public override string Code => "NOT_FOUND";
    public NotFoundException(string message) : base(message) { }
    public static NotFoundException For(string entity, object id) => new($"{entity} '{id}' was not found.");
}

public sealed class ForbiddenException : AppException
{
    public override string Code => "FORBIDDEN";
    public ForbiddenException(string message = "You do not have permission to perform this action.") : base(message) { }
}

public sealed class UnauthorizedException : AppException
{
    public override string Code => "UNAUTHORIZED";
    public UnauthorizedException(string message = "Authentication is required.") : base(message) { }
}

public sealed class ConflictException : AppException
{
    public override string Code => "CONFLICT";
    public ConflictException(string message) : base(message) { }
}

public sealed class ConcurrencyException : AppException
{
    public override string Code => "CONCURRENCY_CONFLICT";
    public ConcurrencyException(string message = "The resource was modified by another request.") : base(message) { }
}

public sealed class DuplicateEntityException : AppException
{
    public override string Code => "DUPLICATE";
    public DuplicateEntityException(string message) : base(message) { }
}

public sealed class ValidationException : AppException
{
    public override string Code => "VALIDATION_ERROR";
    public IReadOnlyDictionary<string, string[]> Errors { get; }
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.") => Errors = errors;
}
