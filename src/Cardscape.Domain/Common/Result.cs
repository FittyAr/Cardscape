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

    public static Result<T> Success<T>(T value) => new(value, null, true);
    public static Result<T> Failure<T>(DomainError error) => new(default, error, false);
}

/// <summary>Generic <see cref="Result"/> that carries a value on success.</summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly DomainError? _error;
    private readonly bool _hasValue;

    public bool IsSuccess => _error is null;
    public bool IsFailure => _error is not null;

    /// <summary>Returns the carried value, or throws if the
    /// result is a failure. Note: a successful <c>Result&lt;T?&gt;</c>
    /// where the value is <c>null</c> still returns <c>null</c>
    /// here — the <c>_hasValue</c> flag, not the value's nullability,
    /// is what distinguishes a successful empty result from a
    /// failure.</summary>
    public T Value
    {
        get
        {
            if (!_hasValue)
            {
                throw new InvalidOperationException("Result has no value; it failed.");
            }

            return _value!;
        }
    }

    public DomainError Error => _error
        ?? throw new InvalidOperationException("Result has no error; it succeeded.");

    internal Result(T? value, DomainError? error, bool hasValue)
    {
        _value = value;
        _error = error;
        _hasValue = hasValue;
    }

    public static Result<T> Success(T value) => new(value, null, true);
    public static Result<T> Failure(DomainError error) => new(default, error, false);

    public static implicit operator Result(Result<T> result) =>
        result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
}
