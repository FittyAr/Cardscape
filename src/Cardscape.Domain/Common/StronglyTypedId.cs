namespace Cardscape.Domain.Common;

/// <summary>
/// Base record for strongly-typed identifiers. Strongly-typed ids
/// prevent passing a <c>BoardId</c> where a <c>CardId</c> is
/// expected, and make the API and the database schema self-describing.
/// </summary>
/// <typeparam name="TSelf">The concrete id type (CRTP).</typeparam>
/// <typeparam name="TValue">Underlying scalar value (typically <see cref="Guid"/>).</typeparam>
public abstract record StronglyTypedId<TSelf, TValue>(TValue Value)
    where TSelf : StronglyTypedId<TSelf, TValue>
    where TValue : notnull
{
    /// <summary>Creates a new id wrapping a freshly generated value.</summary>
    public static TSelf New() => From(default!);

    /// <summary>Wraps an existing scalar value in the strongly-typed id.</summary>
    public static TSelf From(TValue value) => (TSelf)Activator.CreateInstance(typeof(TSelf), value)!;

    public override string ToString() => Value?.ToString() ?? string.Empty;
}
