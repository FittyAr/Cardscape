using Cardscape.Domain.Common;

namespace Cardscape.Domain.Members.Events;

/// <summary>Raised when a new user is registered.</summary>
public sealed record UserRegistered(
    UserId UserId,
    EmailAddress Email,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised after a successful login. Used for audit and "last login" tracking.</summary>
public sealed record UserLoggedIn(
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when the user updates their display name or avatar URL.</summary>
public sealed record UserProfileUpdated(
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when the user changes their password.</summary>
public sealed record UserPasswordChanged(
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a user account is deactivated (soft delete).</summary>
public sealed record UserDeactivated(
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a previously deactivated user is reactivated.
/// Mirrors <see cref="UserDeactivated"/>; emitted by the SCIM
/// flow when an IdP restores an off-boarded user.</summary>
public sealed record UserReactivated(
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a user account is soft-deleted (GDPR Art. 17
/// right-to-erasure grace period starts).</summary>
public sealed record UserSoftDeleted(
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a soft-deleted user is restored within the
/// grace period (the user changed their mind or the request
/// was denied after a manual review).</summary>
public sealed record UserRestored(
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when the retention sweeper (or an admin) clears
/// the PII fields on the user row (GDPR Art. 17 final state).</summary>
public sealed record UserAnonymised(
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a user is flagged as restricted (GDPR Art. 18
/// right-to-restriction). The notification dispatcher reads
/// the flag and skips restricted users.</summary>
public sealed record UserRestricted(
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a user is unflagged from restricted. The
/// notification dispatcher resumes normal fan-out.</summary>
public sealed record UserUnrestricted(
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a user is granted the system-admin role
/// (can access <c>/api/admin/*</c> endpoints). Emitted by
/// the existing-admin grant handler; the seed migration
/// also fires it for the initial admin.</summary>
public sealed record UserGrantedAdmin(
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a user is revoked the system-admin role.</summary>
public sealed record UserRevokedAdmin(
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
