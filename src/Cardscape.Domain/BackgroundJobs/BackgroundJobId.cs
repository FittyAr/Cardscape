namespace Cardscape.Domain.BackgroundJobs;

/// <summary>Identifier of a <see cref="BackgroundJob"/>.</summary>
public sealed record BackgroundJobId(Guid Value) : Common.GuidId<BackgroundJobId>(Value);
