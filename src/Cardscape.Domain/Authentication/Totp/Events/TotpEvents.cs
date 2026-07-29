using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.Authentication.Totp.Events;

/// <summary>Raised when a user enrols 2FA / TOTP for the first time.</summary>
public sealed record TotpCredentialEnrolled(
    TotpCredentialId CredentialId,
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a user successfully verifies a 2FA code.</summary>
public sealed record TotpCredentialVerified(
    TotpCredentialId CredentialId,
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a user disables 2FA / TOTP.</summary>
public sealed record TotpCredentialDisabled(
    TotpCredentialId CredentialId,
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
