namespace Cardscape.Domain.Checklists;

/// <summary>Identifier of a checklist.</summary>
public sealed record ChecklistId(Guid Value) : Common.GuidId<ChecklistId>(Value);
