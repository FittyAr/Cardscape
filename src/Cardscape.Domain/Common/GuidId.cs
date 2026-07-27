namespace Cardscape.Domain.Common;

/// <summary>
/// Strongly-typed <see cref="Guid"/>-based identifier. The vast
/// majority of Cardscape's ids wrap a <see cref="Guid"/>; this
/// base class removes the boilerplate from each concrete id.
/// </summary>
public abstract record GuidId<TSelf>(Guid Value) : StronglyTypedId<TSelf, Guid>(Value)
    where TSelf : GuidId<TSelf>
{
    /// <summary>Generates a new id with a random <see cref="Guid"/>.</summary>
    public static new TSelf New() => (TSelf)Activator.CreateInstance(typeof(TSelf), Guid.NewGuid())!;
}
