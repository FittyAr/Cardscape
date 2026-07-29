using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Infrastructure.Authentication;

/// <summary>
/// Default <see cref="IExternalLoginService"/> implementation.
/// Looks up an existing link by (provider, subject); when
/// none exists, falls back to email matching and (when the
/// email is new) provisions a brand-new user with no
/// password. The new user can later set a password from
/// the Web UI's "Account security" page.
/// </summary>
public sealed class ExternalLoginService(
    IExternalLoginRepository links,
    IUserRepository users,
    IUnitOfWork unitOfWork) : IExternalLoginService
{
    public async Task<Result<ExternalLoginResolution>> ResolveAsync(
        ExternalProvider provider,
        SubjectId subject,
        string? email,
        string? displayName,
        DateTimeOffset at,
        CancellationToken ct)
    {
        // 1) Exact (provider, subject) match.
        var existing = await links.FindByProviderSubjectAsync(provider, subject, ct);
        if (existing is not null)
        {
            existing.RecordUse(email, displayName, at);
            await unitOfWork.SaveChangesAsync(ct);
            var user = await users.GetByIdAsync(existing.UserId, ct)
                ?? throw new InvalidOperationException(
                    $"External login {existing.Id.Value} points at missing user {existing.UserId.Value}.");
            return Result.Success(new ExternalLoginResolution(
                user.Id, existing.Id, IsNewUser: false,
                user.Email.Value, user.DisplayName.Value));
        }

        // 2) Email-based fallback: when the provider grants
        // the email scope and the email already exists in
        // Cardscape, link the new external identity to that
        // existing user. Otherwise auto-provision a new user
        // (only if we have an email to attribute the
        // account to).
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<ExternalLoginResolution>(DomainError.Validation(
                "auth.external.email_required",
                "External provider did not grant the email scope; cannot create a new account."));
        }

        var emailResult = EmailAddress.Create(email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<ExternalLoginResolution>(emailResult.Error);
        }

        var userByEmail = await users.FindByEmailAsync(emailResult.Value.Value, ct);
        if (userByEmail is null)
        {
            // 2a) Brand-new user.
            var displayNameResult = DisplayName.Create(
                string.IsNullOrWhiteSpace(displayName) ? emailResult.Value.Value : displayName);
            if (displayNameResult.IsFailure)
            {
                return Result.Failure<ExternalLoginResolution>(displayNameResult.Error);
            }

            var newUser = User.RegisterExternal(
                UserId.New(),
                emailResult.Value,
                displayNameResult.Value,
                at: at);
            if (newUser.IsFailure)
            {
                return Result.Failure<ExternalLoginResolution>(newUser.Error);
            }

            await users.AddAsync(newUser.Value, ct);

            var newLinkResult = ExternalLogin.Link(
                userId: newUser.Value.Id,
                provider: provider,
                subject: subject,
                email: email,
                displayName: displayName,
                at: at);
            if (newLinkResult.IsFailure)
            {
                return Result.Failure<ExternalLoginResolution>(newLinkResult.Error);
            }

            await links.AddAsync(newLinkResult.Value, ct);
            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success(new ExternalLoginResolution(
                newUser.Value.Id, newLinkResult.Value.Id, IsNewUser: true,
                newUser.Value.Email.Value, newUser.Value.DisplayName.Value));
        }

        // 2b) Existing user — link the new external
        // identity to them.
        var linkResult = ExternalLogin.Link(
            userId: userByEmail.Id,
            provider: provider,
            subject: subject,
            email: email,
            displayName: displayName,
            at: at);
        if (linkResult.IsFailure)
        {
            return Result.Failure<ExternalLoginResolution>(linkResult.Error);
        }

        await links.AddAsync(linkResult.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new ExternalLoginResolution(
            userByEmail.Id, linkResult.Value.Id, IsNewUser: false,
            userByEmail.Email.Value, userByEmail.DisplayName.Value));
    }

    public async Task<IReadOnlyList<ExternalLoginSummary>> ListForUserAsync(
        UserId userId,
        CancellationToken ct)
    {
        var rows = await links.ListForUserAsync(userId, ct);
        return rows
            .Select(r => new ExternalLoginSummary(r.Provider, r.Email, r.DisplayName, r.LastUsedAt))
            .ToList();
    }

    public async Task<Result> UnlinkAsync(
        UserId userId,
        ExternalProvider provider,
        CancellationToken ct)
    {
        var rows = await links.ListForUserAsync(userId, ct);
        var match = rows.FirstOrDefault(r => r.Provider == provider);
        if (match is null)
        {
            return Result.Failure(DomainError.NotFound(
                "auth.external.not_linked",
                "No external identity is linked for that provider."));
        }

        links.Remove(match);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
