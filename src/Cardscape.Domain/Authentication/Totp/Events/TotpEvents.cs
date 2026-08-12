using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.Authentication.Totp.Events;

/// <summary>Raised when a user starts configuring 2FA / TOTP.</summary>
public sealed record TotpEnrollmentStarted(
    TotpCredentialId CredentialId,
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised after the authenticator secret is proven with a valid TOTP.</summary>
public sealed record TotpCredentialConfirmed(
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
