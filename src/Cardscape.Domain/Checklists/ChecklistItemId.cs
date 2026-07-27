namespace Cardscape.Domain.Checklists;

/// <summary>Identifier of a checklist item.</summary>
public sealed record ChecklistItemId(Guid Value) : Common.GuidId<ChecklistItemId>(Value);
