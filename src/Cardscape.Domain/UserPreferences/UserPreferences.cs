using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.UserPreferences.Events;
using static Cardscape.Domain.UserPreferences.Errors.UserPreferencesErrors;

namespace Cardscape.Domain.UserPreferences;

/// <summary>
/// Per-user appearance preferences. One row per
/// <see cref="User"/>, keyed by <see cref="UserId"/>. The
/// aggregate carries the user's chosen theme name
/// (Radzen cookie value, e.g. <c>"default"</c>,
/// <c>"humanistic-dark"</c>, or <c>"cardscape-classic"</c>)
/// and the <see cref="AppearanceMode"/> (Light / Dark /
/// System).
///
/// The aggregate is intentionally minimal: a 1:1 child of
/// <see cref="User"/> that has no children of its own and no
/// other aggregates reference it. The persistence layer
/// stores it in a flat table (<c>user_preferences</c>) with
/// <c>UserId</c> as the primary key. The
/// <c>IUserPreferencesRepository</c> is the only writer.
///
/// GDPR: when the owning user is soft-deleted (Art. 17
/// grace period) or anonymised (Art. 17 final state), the
/// row is hard-deleted by the
/// <c>SoftDeleteUserCommandHandler</c> and
/// <c>AnonymiseUserCommandHandler</c> respectively — same
/// pattern the existing handlers use to drop workspace
/// memberships on the same lifecycle events (see
/// <c>Users/Commands/UserLifecycleCommands.cs</c>).
/// </summary>
public sealed class UserPreferences : AggregateRoot<UserId>
{
    /// <summary>Default theme name applied to a freshly-created
    /// row. Matches Radzen's stock <c>default</c> cookie
    /// value, so the cookie and the server agree on day one.</summary>
    public const string DefaultThemeName = "default";

    private UserPreferences() { }

    private UserPreferences(UserId userId, string themeName, AppearanceMode mode, DateTimeOffset at)
    {
        Id = userId;
        ThemeName = themeName;
        Mode = mode;
        StampCreated(userId.Value, at);
    }

    /// <summary>Radzen cookie value for the chosen theme. One of
    /// the 12 names in
    /// <c>src/Cardscape.Web/Theming/ThemeCatalog.All</c>.</summary>
    public string ThemeName { get; private set; } = DefaultThemeName;

    /// <summary>User's chosen appearance mode.</summary>
    public AppearanceMode Mode { get; private set; } = AppearanceMode.System;

    /// <summary>
    /// Factory: create the preferences row for a brand-new user.
    /// Throws <see cref="Errors.UserPreferencesErrors.AlreadyExists"/>
    /// if the caller did not check uniqueness first — the
    /// repository's <c>AddAsync</c> will surface a
    /// <c>DbUpdateException</c> on the primary-key conflict,
    /// but the application-layer command does the explicit
    /// check via <c>GetByUserIdAsync</c> first to return a
    /// clean 409.
    /// </summary>
    /// <param name="userId">Owning user; this becomes the row's
    /// primary key.</param>
    /// <param name="themeName">Initial theme name. Must be a
    /// valid Radzen cookie value (validated by the
    /// application-layer <c>UpdateUserPreferencesValidator</c>;
    /// the domain trusts its caller).</param>
    /// <param name="mode">Initial appearance mode.</param>
    /// <param name="at">The current UTC time, for
    /// <see cref="Entity{TId}.CreatedAt"/>.</param>
    public static Result<UserPreferences> Create(
        UserId userId,
        string themeName,
        AppearanceMode mode,
        DateTimeOffset at)
    {
        if (userId is null)
        {
            return Result.Failure<UserPreferences>(DomainError.Validation(
                "members.user_preferences.user_required",
                "User id is required."));
        }

        if (string.IsNullOrWhiteSpace(themeName))
        {
            return Result.Failure<UserPreferences>(DomainError.Validation(
                "members.user_preferences.theme_required",
                "Theme name is required."));
        }

        UserPreferences prefs = new(userId, themeName.Trim(), mode, at);
        prefs.AddDomainEvent(new UserPreferencesCreated(userId, themeName, mode, at));
        return Result.Success(prefs);
    }

    /// <summary>
    /// Apply a new theme name and/or mode. The two fields
    /// are independently optional so the call site can update
    /// just one of them without resending the other. The
    /// caller is expected to have validated
    /// <paramref name="themeName"/> against the catalogue of
    /// valid Radzen cookie values (the application-layer
    /// <c>UpdateUserPreferencesValidator</c> does this);
    /// passing an unknown value here still surfaces a clean
    /// <see cref="Errors.UserPreferencesErrors.InvalidThemeName"/>
    /// so the domain is defensive.
    /// </summary>
    public Result Update(
        string? themeName,
        AppearanceMode? mode,
        IReadOnlyCollection<string> validThemeNames,
        DateTimeOffset at)
    {
        bool changed = false;

        if (themeName is not null)
        {
            string trimmed = themeName.Trim();
            if (!validThemeNames.Contains(trimmed))
            {
                return Result.Failure(InvalidThemeName);
            }

            if (trimmed != ThemeName)
            {
                ThemeName = trimmed;
                changed = true;
            }
        }

        if (mode is not null && mode.Value != Mode)
        {
            if (!Enum.IsDefined(mode.Value))
            {
                return Result.Failure(InvalidMode);
            }

            Mode = mode.Value;
            changed = true;
        }

        if (!changed)
        {
            return Result.Success();
        }

        StampChanged(Id.Value, at);
        AddDomainEvent(new UserPreferencesUpdated(Id, ThemeName, Mode, at));
        return Result.Success();
    }
}
