using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Checklists.Events;

/// <summary>Raised when a checklist is added to a card.</summary>
public sealed record ChecklistCreated(
    ChecklistId ChecklistId,
    CardId CardId,
    ChecklistTitle Title,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a checklist is renamed.</summary>
public sealed record ChecklistRenamed(
    ChecklistId ChecklistId,
    ChecklistTitle NewTitle,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a checklist is deleted.</summary>
public sealed record ChecklistDeleted(
    ChecklistId ChecklistId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a checklist item is added.</summary>
public sealed record ChecklistItemAdded(
    ChecklistId ChecklistId,
    ChecklistItemId ItemId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a checklist item is checked.</summary>
public sealed record ChecklistItemChecked(
    ChecklistId ChecklistId,
    ChecklistItemId ItemId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a checklist item is unchecked.</summary>
public sealed record ChecklistItemUnchecked(
    ChecklistId ChecklistId,
    ChecklistItemId ItemId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a checklist item is renamed or its text changes.</summary>
public sealed record ChecklistItemUpdated(
    ChecklistId ChecklistId,
    ChecklistItemId ItemId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a checklist item is removed.</summary>
public sealed record ChecklistItemDeleted(
    ChecklistId ChecklistId,
    ChecklistItemId ItemId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
