namespace Library.Application.Common;

/// <summary>
/// The kind of business failure. The API maps each value to an HTTP status code (404, 400, 409, 403, 401).
/// </summary>
public enum ErrorType
{
    NotFound,
    Validation,
    Conflict,
    Forbidden,
    Unauthorized
}

/// <summary>
/// An expected business failure returned by a service, with a message that is shown to the client.
/// </summary>
public sealed record Error(ErrorType Type, string Message)
{
    public static Error NotFound(string message) => new(ErrorType.NotFound, message);

    public static Error Validation(string message) => new(ErrorType.Validation, message);

    public static Error Conflict(string message) => new(ErrorType.Conflict, message);

    public static Error Forbidden(string message) => new(ErrorType.Forbidden, message);

    public static Error Unauthorized(string message) => new(ErrorType.Unauthorized, message);
}
