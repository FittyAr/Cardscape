namespace Cardscape.Domain.Common;

/// <summary>
/// The Result monad. Use it instead of throwing exceptions for
/// expected failures (validation, not-found, conflict, forbidden).
/// Unexpected failures (database down, programmer error) still
/// propagate as exceptions and are handled by the global middleware.
/// </summary>
public readonly struct Result
{
    private readonly DomainError? _error;

    public bool IsSuccess => _error is null;
    public bool IsFailure => _error is not null;

    public DomainError Error => _error
        ?? throw new InvalidOperationException("Result has no error; it succeeded.");

    private Result(DomainError? error) => _error = error;

    public static Result Success() => new(null);
    public static Result Failure(DomainError error) => new(error);

    public static Result<T> Success<T>(T value) => new(value, null);
    public static Result<T> Failure<T>(DomainError error) => new(default, error);
}

/// <summary>Generic <see cref="Result"/> that carries a value on success.</summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly DomainError? _error;

    public bool IsSuccess => _error is null;
    public bool IsFailure => _error is not null;

    public T Value => _value
        ?? throw new InvalidOperationException("Result has no value; it failed.");

    public DomainError Error => _error
        ?? throw new InvalidOperationException("Result has no error; it succeeded.");

    internal Result(T? value, DomainError? error)
    {
        _value = value;
        _error = error;
    }

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(DomainError error) => new(default, error);

    public static implicit operator Result(Result<T> result) =>
        result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
}
