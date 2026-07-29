namespace Cardscape.Domain.Recurrence;

public sealed record CardRecurrenceId(Guid Value) : Common.GuidId<CardRecurrenceId>(Value);
