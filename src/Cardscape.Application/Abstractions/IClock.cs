namespace Cardscape.Application.Abstractions;

/// <summary>
/// Abstraction over <see cref="DateTimeOffset.UtcNow"/>. Used by
/// handlers and entities so tests can pin time deterministically.
/// </summary>
public interface IClock
{
    /// <summary>Returns the current UTC time.</summary>
    DateTimeOffset UtcNow { get; }
}
