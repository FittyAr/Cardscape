using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Labels.Events;

/// <summary>Raised when a label is created on a board.</summary>
public sealed record LabelCreated(
    LabelId LabelId,
    BoardId BoardId,
    LabelName Name,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a label is renamed or recolored.</summary>
public sealed record LabelUpdated(
    LabelId LabelId,
    LabelName NewName,
    Common.Color NewColor,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a label is deleted.</summary>
public sealed record LabelDeleted(
    LabelId LabelId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
