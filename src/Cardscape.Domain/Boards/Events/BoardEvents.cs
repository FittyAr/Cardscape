using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Domain.Boards.Events;

/// <summary>Raised when a board is created.</summary>
public sealed record BoardCreated(
    BoardId BoardId,
    WorkspaceId WorkspaceId,
    BoardName Name,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a board is renamed.</summary>
public sealed record BoardRenamed(
    BoardId BoardId,
    BoardName NewName,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a board's description is changed.</summary>
public sealed record BoardDescriptionChanged(
    BoardId BoardId,
    BoardDescription NewDescription,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a board's visibility changes.</summary>
public sealed record BoardVisibilityChanged(
    BoardId BoardId,
    BoardVisibility NewVisibility,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a board is archived.</summary>
public sealed record BoardArchived(
    BoardId BoardId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a board is un-archived.</summary>
public sealed record BoardUnarchived(
    BoardId BoardId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a member is added to a board.</summary>
public sealed record BoardMemberAdded(
    BoardId BoardId,
    Guid UserId,
    BoardMemberRole Role,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a member is removed from a board.</summary>
public sealed record BoardMemberRemoved(
    BoardId BoardId,
    Guid UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a board is starred by a user.</summary>
public sealed record BoardStarred(
    BoardId BoardId,
    Guid UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a board is unstarred by a user.</summary>
public sealed record BoardUnstarred(
    BoardId BoardId,
    Guid UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
