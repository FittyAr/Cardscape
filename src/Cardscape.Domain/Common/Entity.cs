namespace Cardscape.Domain.Common;

/// <summary>
/// Base class for any entity in the domain. An entity is identified
/// by its id (not by its attributes) and has a continuous life
/// cycle.
/// </summary>
/// <typeparam name="TId">Strongly-typed identifier of the entity.</typeparam>
public abstract class Entity<TId>
    where TId : notnull
{
    /// <summary>Entity identifier.</summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>UTC timestamp at which the entity was created.</summary>
    public DateTimeOffset CreatedAt { get; protected set; }

    /// <summary>UTC timestamp of the last modification, or <c>null</c> if never modified.</summary>
    public DateTimeOffset? UpdatedAt { get; protected set; }

    /// <summary>
    /// Stamps the entity as modified right now by the given user and
    /// bumps the optimistic-concurrency token. Application-layer
    /// handlers should call this after they mutate the aggregate.
    /// </summary>
    public void StampChanged(Guid? by, DateTimeOffset at)
    {
        UpdatedAt = at;
        UpdatedBy = by;
        RowVersion++;
    }

    /// <summary>Identifier of the user that created the entity, when known.</summary>
    public Guid? CreatedBy { get; protected set; }

    /// <summary>Identifier of the user that last modified the entity, when known.</summary>
    public Guid? UpdatedBy { get; protected set; }

    /// <summary>
    /// Optimistic-concurrency token used by EF Core. Starts at 0 on
    /// construction; every state-changing method (or its handler)
    /// bumps it via <see cref="StampChanged"/>. SQLite has no native
    /// rowversion, so we use a managed <c>uint</c> with a DB-side
    /// default of 0 (see <c>*Configuration.cs</c>).
    /// </summary>
    public uint RowVersion { get; protected set; }

    /// <summary>Soft-delete flag. When true, the row is hidden from
    /// default queries but kept in the table for audit purposes.</summary>
    public bool IsDeleted { get; protected set; }

    /// <summary>
    /// Stamps the entity as created right now by the given user.
    /// Called by repositories or handlers right before insertion.
    /// </summary>
    public void StampCreated(Guid? by, DateTimeOffset at)
    {
        CreatedAt = at;
        CreatedBy = by;
        UpdatedAt = null;
        UpdatedBy = null;
    }

    /// <summary>Stamps the entity as last modified right now by the given user.</summary>
    public void StampUpdated(Guid? by, DateTimeOffset at)
    {
        UpdatedAt = at;
        UpdatedBy = by;
    }

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override int GetHashCode() => Id.GetHashCode();
}
