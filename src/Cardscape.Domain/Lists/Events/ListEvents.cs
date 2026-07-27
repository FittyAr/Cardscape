using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Lists.Events;

/// <summary>Raised when a list is created on a board.</summary>
public sealed record ListCreated(
    BoardListId ListId,
    BoardId BoardId,
    ListName Name,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a list is renamed.</summary>
public sealed record ListRenamed(
    BoardListId ListId,
    ListName NewName,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a list's position changes (move, drag, reorder).</summary>
public sealed record ListMoved(
    BoardListId ListId,
    Position NewPosition,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a list is archived.</summary>
public sealed record ListArchived(
    BoardListId ListId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when an archived list is restored.</summary>
public sealed record ListRestored(
    BoardListId ListId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
