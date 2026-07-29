using Cardscape.Domain.Authentication.ExternalLogins.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.Authentication.ExternalLogins;

/// <summary>
/// The (user, provider, subject) tuple that links a Cardscape
/// user to their external identity. One user can have many
/// rows of this table (one per provider); the (provider,
/// subject) pair is unique so the same external identity
/// can never be claimed by two different Cardscape users.
/// </summary>
public sealed class ExternalLogin : AggregateRoot<ExternalLoginId>
{
    /// <summary>Owner of the link (the Cardscape user).</summary>
    public UserId UserId { get; private set; } = null!;

    /// <summary>External provider (Google, Microsoft, Apple).</summary>
    public ExternalProvider Provider { get; private set; }

    /// <summary>Provider-assigned subject id.</summary>
    public SubjectId Subject { get; private set; } = null!;

    /// <summary>Last email the provider returned for this subject. <c>null</c> if the user has never granted the email scope.</summary>
    public string? Email { get; private set; }

    /// <summary>Last display name the provider returned. <c>null</c> if the user has never granted the profile scope.</summary>
    public string? DisplayName { get; private set; }

    /// <summary>Last time the user signed in with this external identity.</summary>
    public DateTimeOffset LastUsedAt { get; private set; }

    // EF Core.
    private ExternalLogin() { }

    private ExternalLogin(
        ExternalLoginId id,
        UserId userId,
        ExternalProvider provider,
        SubjectId subject,
        string? email,
        string? displayName,
        DateTimeOffset at)
    {
        Id = id;
        UserId = userId;
        Provider = provider;
        Subject = subject;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        LastUsedAt = at;
        CreatedAt = at;
    }

    /// <summary>
    /// Links a new external identity to <paramref name="userId"/>.
    /// Returns a domain error if the inputs are invalid
    /// (empty subject, etc.).
    /// </summary>
    public static Result<ExternalLogin> Link(
        UserId userId,
        ExternalProvider provider,
        SubjectId subject,
        string? email,
        string? displayName,
        DateTimeOffset at)
    {
        var link = new ExternalLogin(
            id: ExternalLoginId.New(),
            userId: userId,
            provider: provider,
            subject: subject,
            email: email,
            displayName: displayName,
            at: at);
        link.AddDomainEvent(new ExternalLoginLinked(
            link.Id, userId, provider, subject, at));
        return Result.Success(link);
    }

    /// <summary>
    /// Records a successful external login. Updates
    /// <see cref="Email"/> / <see cref="DisplayName"/> if the
    /// provider returned a fresh value, and bumps
    /// <see cref="LastUsedAt"/>.
    /// </summary>
    public void RecordUse(string? email, string? displayName, DateTimeOffset at)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            Email = email.Trim();
        }
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }
        LastUsedAt = at;
        UpdatedAt = at;
        AddDomainEvent(new ExternalLoginRecorded(Id, UserId, Provider, at));
    }
}
