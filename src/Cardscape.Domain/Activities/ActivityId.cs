namespace Cardscape.Domain.Activities;

/// <summary>Identifier of an activity entry.</summary>
public sealed record ActivityId(Guid Value) : Common.GuidId<ActivityId>(Value);
