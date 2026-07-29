namespace Cardscape.Domain.Dashboards;

public sealed record DashcardId(Guid Value) : Common.GuidId<DashcardId>(Value);
