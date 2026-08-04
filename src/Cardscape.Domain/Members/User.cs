using Cardscape.Domain.Common;
using Cardscape.Domain.Members.Errors;
using Cardscape.Domain.Members.Events;
using static Cardscape.Domain.Members.Errors.UserErrors;

namespace Cardscape.Domain.Members;

/// <summary>
/// A registered Cardscape user. Authentication and identity are
/// the responsibilities of this aggregate; everything else (which
/// workspaces the user belongs to, which boards, etc.) is modelled
/// in the corresponding contexts.
/// </summary>
public sealed class User : AggregateRoot<UserId>
{
    /// <summary>Validated, lower-cased email address.</summary>
    public EmailAddress Email { get; private set; } = null!;

    /// <summary>User-visible display name.</summary>
    public DisplayName DisplayName { get; private set; } = null!;

    /// <summary>Opaque, algorithm-tagged password hash.</summary>
    public PasswordHash PasswordHash { get; private set; } = null!;

    /// <summary>Optional URL to the user's avatar image.</summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>UTC timestamp of the last successful login, or <c>null</c> if the user has never logged in.</summary>
    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>Whether the account is active. Deactivated users cannot sign in.</summary>
    public bool IsActive { get; private set; } = true;

    // ── GDPR (Art. 5, 17, 18, 21) ─────────────────────────────
    // The aggregate carries the four flags + their timestamps
    // that the GDPR endpoint, the retention sweeper, and the
    // audit log all read. The flags are independent:
    //   • IsDeleted = true  → soft-deleted (Art. 17 grace period)
    //   • IsAnonymised = true → PII cleared (Art. 17 final state)
    //   • IsRestricted = true → read-only (Art. 18)
    //   • IsAdmin = true → can access /api/admin/* endpoints
    // A user can be restricted and active at the same time
    // (the controller decided to restrict processing, but the
    // user is still in the workspace).

    /// <summary>True after <see cref="SoftDelete"/> has been called.
    /// The user cannot sign in and is hidden from the
    /// directory, but the record stays for the 30-day
    /// grace period before the retention sweeper hard-deletes
    /// the row. The flag shadows the base
    /// <see cref="Entity{TId}.IsDeleted"/>; the aggregate
    /// is the authoritative writer.</summary>
    public new bool IsDeleted { get; private set; }

    /// <summary>UTC timestamp of the soft-delete, or <c>null</c>.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>True after <see cref="Anonymise"/> has been called.
    /// The PII fields (email, display name, password hash,
    /// avatar URL) are replaced with non-personalised
    /// placeholders; the row is kept (no longer "personal
    /// data" under GDPR Art. 4(1)) so the audit log and the
    /// foreign keys from cards / comments / etc. still
    /// resolve.</summary>
    public bool IsAnonymised { get; private set; }

    /// <summary>UTC timestamp of the anonymisation, or <c>null</c>.</summary>
    public DateTimeOffset? AnonymisedAt { get; private set; }

    /// <summary>True after <see cref="SetRestricted"/> has been called.
    /// The user can read but not write.</summary>
    public bool IsRestricted { get; private set; }

    /// <summary>UTC timestamp of the restriction, or <c>null</c>.</summary>
    public DateTimeOffset? RestrictedAt { get; private set; }

    /// <summary>True for system administrators. The AdminOnly
    /// policy in the API consults this flag; non-admin
    /// authenticated users get 403 on <c>/api/admin/*</c>
    /// endpoints. The flag is set explicitly by an
    /// existing admin (or seeded by a migration); the
    /// regular self-service registration flow does
    /// not set it.</summary>
    public bool IsAdmin { get; private set; }

    // EF Core.
    private User() { }

    private User(
        UserId id,
        EmailAddress email,
        DisplayName displayName,
        PasswordHash passwordHash)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        IsActive = true;
    }

    /// <summary>
    /// Factory: register a new user. Email and display name are
    /// validated by the value objects; the password is already
    /// hashed by the time it gets here (the application layer is
    /// responsible for hashing).
    /// </summary>
    public static Result<User> Register(
        UserId id,
        EmailAddress email,
        DisplayName displayName,
        PasswordHash passwordHash,
        DateTimeOffset at)
    {
        if (id.Value == Guid.Empty)
        {
            return Result.Failure<User>(DomainError.Validation(
                "members.user.id_required",
                "User id is required."));
        }

        var user = new User(id, email, displayName, passwordHash)
        {
            CreatedAt = at
        };
        user.AddDomainEvent(new UserRegistered(id, email, at));
        return Result.Success(user);
    }

    /// <summary>
    /// Factory: register a user that has no password because
    /// they signed in via an external OAuth provider (Google,
    /// Microsoft, Apple). The user can later set a password
    /// from the Web UI's "Account security" page via
    /// <see cref="ChangePassword"/>.
    /// </summary>
    public static Result<User> RegisterExternal(
        UserId id,
        EmailAddress email,
        DisplayName displayName,
        DateTimeOffset at)
    {
        if (id.Value == Guid.Empty)
        {
            return Result.Failure<User>(DomainError.Validation(
                "members.user.id_required",
                "User id is required."));
        }

        // The placeholder hash is a fixed, non-empty value
        // so the row passes the NOT NULL constraint. It is
        // never used for authentication because the
        // external-login flow never calls Verify on it.
        var placeholderHash = PasswordHash.FromHashed("EXTERNAL::" + Guid.NewGuid().ToString("N")).Value;
        var user = new User(id, email, displayName, placeholderHash)
        {
            CreatedAt = at
        };
        user.AddDomainEvent(new UserRegistered(id, email, at));
        return Result.Success(user);
    }

    /// <summary>Marks a successful login and stamps <see cref="LastLoginAt"/>.</summary>
    public void RecordLogin(DateTimeOffset at)
    {
        if (!IsActive)
        {
            return;
        }

        LastLoginAt = at;
        UpdatedAt = at;
        AddDomainEvent(new UserLoggedIn(Id, at));
    }

    /// <summary>Updates the display name and avatar URL.</summary>
    public Result UpdateProfile(DisplayName newDisplayName, string? newAvatarUrl, DateTimeOffset at)
    {
        if (newDisplayName.Value == DisplayName.Value
            && string.Equals(newAvatarUrl, AvatarUrl, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        DisplayName = newDisplayName;
        AvatarUrl = string.IsNullOrWhiteSpace(newAvatarUrl) ? null : newAvatarUrl.Trim();
        UpdatedAt = at;
        AddDomainEvent(new UserProfileUpdated(Id, at));
        return Result.Success();
    }

    /// <summary>Replaces the stored password hash.</summary>
    public void ChangePassword(PasswordHash newHash, DateTimeOffset at)
    {
        PasswordHash = newHash;
        UpdatedAt = at;
        AddDomainEvent(new UserPasswordChanged(Id, at));
    }

    /// <summary>Deactivates the account. The user can no longer sign in.</summary>
    public void Deactivate(DateTimeOffset at)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedAt = at;
        AddDomainEvent(new UserDeactivated(Id, at));
    }

    /// <summary>Reactivates a previously deactivated account. Used by
    /// SCIM when an IdP sends <c>{ "op": "replace", "path": "active", "value": true }</c>
    /// for an off-boarded user.</summary>
    public void Reactivate(DateTimeOffset at)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        UpdatedAt = at;
        AddDomainEvent(new UserReactivated(Id, at));
    }

    // ── GDPR (Art. 17, 18) ───────────────────────────────────

    /// <summary>
    /// Soft-deletes the account. The user cannot sign in
    /// (sets <see cref="IsActive"/> to <c>false</c>),
    /// the record is marked deleted, and the
    /// <see cref="DeletedAt"/> timestamp is stamped.
    /// The 30-day grace period starts here; after it
    /// elapses, the retention sweeper hard-deletes the
    /// row.
    /// </summary>
    public void SoftDelete(DateTimeOffset at)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        IsActive = false;
        DeletedAt = at;
        UpdatedAt = at;
        AddDomainEvent(new UserSoftDeleted(Id, at));
    }

    /// <summary>
    /// Reverses a soft-delete within the grace period.
    /// Restores <see cref="IsActive"/>; the
    /// <see cref="IsDeleted"/> flag flips back to false.
    /// Outside the grace period the retention sweeper
    /// has already removed the row, so the call is
    /// a no-op on a hard-deleted user.
    /// </summary>
    public void Restore(DateTimeOffset at)
    {
        if (!IsDeleted)
        {
            return;
        }

        IsDeleted = false;
        IsActive = true;
        DeletedAt = null;
        UpdatedAt = at;
        AddDomainEvent(new UserRestored(Id, at));
    }

    /// <summary>
    /// Clears every PII field on the row. The user's
    /// email is replaced with <c>anonymised-{id}@anonymised.local</c>,
    /// the display name with <c>Anonymised user</c>, the
    /// password hash with a fresh random opaque value
    /// (the user can never sign in again — by design), and
    /// the avatar URL is cleared. The user id stays
    /// so the audit log and the foreign keys
    /// from cards / comments / etc. still resolve.
    /// This is the final state after the right-to-erasure
    /// grace period elapses.
    /// </summary>
    public void Anonymise(DateTimeOffset at)
    {
        if (IsAnonymised)
        {
            return;
        }

        Email = EmailAddress.Create($"anonymised-{Id.Value:N}@anonymised.local").Value;
        DisplayName = DisplayName.Create("Anonymised user").Value;
        PasswordHash = PasswordHash.FromHashed("ANONYMISED::" + Guid.NewGuid().ToString("N")).Value;
        AvatarUrl = null;
        IsAnonymised = true;
        IsActive = false;
        AnonymisedAt = at;
        UpdatedAt = at;
        AddDomainEvent(new UserAnonymised(Id, at));
    }

    /// <summary>
    /// Sets or clears the <see cref="IsRestricted"/> flag.
    /// The flag is the GDPR Art. 18 "right to restriction"
    /// surface: the controller grants the request, the
    /// user can read but cannot write. The flag is also
    /// the GDPR Art. 21 "right to object" surface for the
    /// notification dispatcher (a restricted user is
    /// skipped by the notification fan-out).
    /// </summary>
    public void SetRestricted(bool restricted, DateTimeOffset at)
    {
        if (IsRestricted == restricted)
        {
            return;
        }

        IsRestricted = restricted;
        RestrictedAt = restricted ? at : null;
        UpdatedAt = at;
        AddDomainEvent(restricted
            ? (DomainEventBase)new UserRestricted(Id, at)
            : new UserUnrestricted(Id, at));
    }

    // ── Admin role (system-level) ────────────────────────────

    /// <summary>
    /// Sets or clears the <see cref="IsAdmin"/> flag. Only
    /// the existing admin (or the seed migration) can call
    /// this; the application-layer handler enforces the
    /// caller-is-admin rule before invoking the aggregate
    /// method.
    /// </summary>
    public void SetAdmin(bool isAdmin, DateTimeOffset at)
    {
        if (IsAdmin == isAdmin)
        {
            return;
        }

        IsAdmin = isAdmin;
        UpdatedAt = at;
        AddDomainEvent(isAdmin
            ? (DomainEventBase)new UserGrantedAdmin(Id, at)
            : new UserRevokedAdmin(Id, at));
    }
}
