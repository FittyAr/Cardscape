using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.UserPreferences.Events;

/// <summary>Raised when a user's preferences row is first
/// created (typically on first login; the row is lazily
/// provisioned by the API endpoint). Used by the audit
/// log and the GDPR retention sweeper.</summary>
public sealed record UserPreferencesCreated(
    UserId UserId,
    string ThemeName,
    AppearanceMode Mode,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when the user changes their theme or
/// mode. Carries the new values so downstream consumers
/// (audit log, analytics) do not need to re-query.</summary>
public sealed record UserPreferencesUpdated(
    UserId UserId,
    string ThemeName,
    AppearanceMode Mode,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
