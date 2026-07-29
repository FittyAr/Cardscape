using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.Authentication.ExternalLogins.Events;

/// <summary>
/// Raised the first time a user signs in with a particular
/// external provider. The <see cref="UserId"/> is the
/// newly-linked Cardscape user (which may be a brand-new
/// account created on the fly, or an existing account that
/// just connected its first external identity).
/// </summary>
public sealed record ExternalLoginLinked(
    ExternalLoginId LinkId,
    UserId UserId,
    ExternalProvider Provider,
    SubjectId Subject,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>
/// Raised on every successful external login (whether the
/// link is new or returning). The application layer
/// translates this into a JWT issuance.
/// </summary>
public sealed record ExternalLoginRecorded(
    ExternalLoginId LinkId,
    UserId UserId,
    ExternalProvider Provider,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
