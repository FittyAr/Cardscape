namespace Cardscape.Domain.Common;

/// <summary>
/// Error categories used by the application to map domain failures
/// to HTTP status codes (or to MCP error codes).
/// </summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unauthenticated
}

/// <summary>
/// A self-contained, immutable domain error. Carries a stable code
/// (for the wire) and a human-readable message (for logs and
/// internal API responses).
/// </summary>
public sealed record DomainError(ErrorType Type, string Code, string Message)
{
    public static DomainError Validation(string code, string message) =>
        new(ErrorType.Validation, code, message);

    public static DomainError NotFound(string code, string message) =>
        new(ErrorType.NotFound, code, message);

    public static DomainError Conflict(string code, string message) =>
        new(ErrorType.Conflict, code, message);

    public static DomainError Forbidden(string code, string message) =>
        new(ErrorType.Forbidden, code, message);

    public static DomainError Unauthenticated(string code, string message) =>
        new(ErrorType.Unauthenticated, code, message);
}
