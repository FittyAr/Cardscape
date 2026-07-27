using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;

namespace Cardscape.Domain.Cards.Events;

/// <summary>Raised when a card is created on a list.</summary>
public sealed record CardCreated(
    CardId CardId,
    BoardListId ListId,
    CardTitle Title,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a card is renamed.</summary>
public sealed record CardRenamed(
    CardId CardId,
    CardTitle NewTitle,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a card's description is changed.</summary>
public sealed record CardDescriptionChanged(
    CardId CardId,
    CardDescription NewDescription,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a card is moved (within a list or to a different list).</summary>
public sealed record CardMoved(
    CardId CardId,
    BoardListId NewListId,
    Position NewPosition,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a card is archived.</summary>
public sealed record CardArchived(
    CardId CardId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when an archived card is restored.</summary>
public sealed record CardRestored(
    CardId CardId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a due date is set on a card.</summary>
public sealed record CardDueDateSet(
    CardId CardId,
    DateTimeOffset DueDate,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a due date is cleared.</summary>
public sealed record CardDueDateCleared(
    CardId CardId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a card is completed (marked as done).</summary>
public sealed record CardCompleted(
    CardId CardId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a completed card is reopened.</summary>
public sealed record CardReopened(
    CardId CardId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
