namespace FiapGames.Shared.Kernel.Results;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized
}

public sealed record Error(ErrorType Type, string Message)
{
    public static Error NotFound(string message) => new(ErrorType.NotFound, message);

    public static Error Validation(string message) => new(ErrorType.Validation, message);

    public static Error Conflict(string message) => new(ErrorType.Conflict, message);

    public static Error Unauthorized(string message) => new(ErrorType.Unauthorized, message);
}
