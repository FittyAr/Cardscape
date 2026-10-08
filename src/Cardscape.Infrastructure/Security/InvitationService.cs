using System.Security.Cryptography;
using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Infrastructure.Security;

/// <summary>
/// Implementation of <see cref="IInvitationService"/>. Owns the
/// token-generation and validation logic that the domain
/// deliberately knows nothing about: random byte generation,
/// SHA-256 hashing, base64url encoding. The cleartext secret is
/// returned to the caller exactly once at issuance and is never
/// persisted or logged.
/// </summary>
public sealed class InvitationService(
    IWorkspaceInvitationRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock) : IInvitationService
{
    public async Task<WorkspaceInvitationIssuance> IssueAsync(
        WorkspaceId workspaceId,
        string email,
        WorkspaceRole role,
        Guid invitedBy,
        TimeSpan? lifetime,
        CancellationToken ct)
    {
        var (cleartext, hashed, prefix) = SecureToken.Generate(InvitationToken.CleartextByteLength, InvitationToken.PrefixLength);

        var creation = WorkspaceInvitation.Issue(
            workspaceId: workspaceId,
            email: email,
            role: role,
            invitedBy: invitedBy,
            tokenHash: hashed,
            tokenPrefix: prefix,
            at: clock.UtcNow,
            lifetime: lifetime);

        if (creation.IsFailure)
        {
            throw new InvalidOperationException(creation.Error.Message);
        }

        await repository.AddAsync(creation.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new WorkspaceInvitationIssuance(creation.Value.Id, cleartext);
    }

    public async Task<Result<WorkspaceInvitationValidation>> ValidateAsync(
        string cleartextToken, DateTimeOffset now, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cleartextToken))
        {
            return Result.Failure<WorkspaceInvitationValidation>(DomainError.Validation(
                "workspaces.invitation.token_required",
                "Invitation token is required."));
        }

        var hashed = SecureToken.HashHex(cleartextToken);
        var invitation = await repository.FindByTokenHashAsync(hashed, ct);
        if (invitation is null)
        {
            return Result.Failure<WorkspaceInvitationValidation>(DomainError.NotFound(
                "workspaces.invitation.not_found", "Invitation was not found."));
        }

        Result redeemable = invitation.EnsureRedeemable(now);
        if (redeemable.IsFailure)
        {
            return Result.Failure<WorkspaceInvitationValidation>(redeemable.Error);
        }

        return Result.Success(new WorkspaceInvitationValidation(
            invitation.Id,
            invitation.WorkspaceId,
            invitation.Role,
            invitation.Email));
    }
}
