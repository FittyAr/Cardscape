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
        var (cleartext, hashed, prefix) = GenerateToken();

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

        var hashed = HashToken(cleartextToken);
        var invitation = await repository.FindByTokenHashAsync(hashed, ct);
        if (invitation is null)
        {
            return Result.Failure<WorkspaceInvitationValidation>(DomainError.NotFound(
                "workspaces.invitation.not_found", "Invitation was not found."));
        }

        if (!invitation.IsActive(now))
        {
            // Distinguish revoked / accepted / expired for a
            // slightly nicer error message.
            if (invitation.AcceptedAt is not null)
            {
                return Result.Failure<WorkspaceInvitationValidation>(DomainError.Conflict(
                    "workspaces.invitation.already_accepted",
                    "Invitation has already been accepted."));
            }

            if (invitation.RevokedAt is not null)
            {
                return Result.Failure<WorkspaceInvitationValidation>(DomainError.Conflict(
                    "workspaces.invitation.revoked", "Invitation has been revoked."));
            }

            return Result.Failure<WorkspaceInvitationValidation>(DomainError.Forbidden(
                "workspaces.invitation.expired", "Invitation has expired."));
        }

        return Result.Success(new WorkspaceInvitationValidation(
            invitation.Id,
            invitation.WorkspaceId,
            invitation.Role,
            invitation.Email));
    }

    private static (string cleartext, string hashed, string prefix) GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[InvitationToken.CleartextByteLength];
        RandomNumberGenerator.Fill(bytes);
        var cleartext = Base64UrlEncode(bytes);
        var hashed = HashToken(cleartext);
        var prefix = cleartext[..Math.Min(InvitationToken.PrefixLength, cleartext.Length)];
        return (cleartext, hashed, prefix);
    }

    private static string HashToken(string cleartext)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(cleartext), hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var b64 = Convert.ToBase64String(bytes);
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
