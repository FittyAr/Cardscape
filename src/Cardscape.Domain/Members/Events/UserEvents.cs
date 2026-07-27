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
