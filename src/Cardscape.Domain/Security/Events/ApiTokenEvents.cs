using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.Security.Events;

/// <summary>Raised when a new <see cref="ApiToken"/> is issued.</summary>
public sealed record ApiTokenIssued(
    ApiTokenId TokenId,
    UserId UserId,
    ApiTokenName Name,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when an <see cref="ApiToken"/> is revoked.</summary>
public sealed record ApiTokenRevoked(
    ApiTokenId TokenId,
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
