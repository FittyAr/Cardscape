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
}
