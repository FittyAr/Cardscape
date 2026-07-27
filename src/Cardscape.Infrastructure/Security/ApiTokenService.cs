using System.Security.Cryptography;
using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Security;

namespace Cardscape.Infrastructure.Security;

/// <summary>
/// Implementation of <see cref="IApiTokenService"/>. Owns the
/// secret-generation logic that the domain deliberately knows
/// nothing about: random byte generation, SHA-256 hashing, and
/// the cleartext/prefix split. The cleartext secret is returned
/// to the caller exactly once at issuance and is never
/// persisted or logged.
/// </summary>
public sealed class ApiTokenService(
    IApiTokenRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock) : IApiTokenService
{
    public async Task<ApiTokenIssuance> IssueAsync(
        UserId userId,
        string name,
        IReadOnlyCollection<string> scopes,
        DateTimeOffset? expiresAt,
        CancellationToken ct)
    {
        var nameResult = ApiTokenName.Create(name);
        if (nameResult.IsFailure)
        {
            throw new InvalidOperationException(nameResult.Error.Message);
        }

        var scopesResult = ApiTokenScopes.Create(scopes);
        if (scopesResult.IsFailure)
        {
            throw new InvalidOperationException(scopesResult.Error.Message);
        }

        var (cleartext, hashed, prefix) = GenerateSecret();

        var creation = ApiToken.Create(
            userId,
            nameResult.Value,
            hashed,
            prefix,
            scopesResult.Value,
            expiresAt,
            clock.UtcNow);

        if (creation.IsFailure)
        {
            throw new InvalidOperationException(creation.Error.Message);
        }

        await repository.AddAsync(creation.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ApiTokenIssuance(creation.Value.Id, cleartext);
    }

    public async Task<Result<ApiTokenValidation>> ValidateAsync(string cleartextSecret, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cleartextSecret))
        {
            return Result.Failure<ApiTokenValidation>(DomainError.Unauthenticated(
                "auth.required", "Bearer secret is required."));
        }

        var hashed = HashSecret(cleartextSecret);
        var token = await repository.FindByHashedSecretAsync(hashed, ct);
        if (token is null)
        {
            return Result.Failure<ApiTokenValidation>(DomainError.Unauthenticated(
                "auth.invalid_token", "API token is invalid."));
        }

        var now = clock.UtcNow;
        if (!token.IsActive(now))
        {
            return Result.Failure<ApiTokenValidation>(DomainError.Forbidden(
                "auth.token_inactive",
                token.RevokedAt is not null
                    ? "API token has been revoked."
                    : "API token has expired."));
        }

        token.RecordUse(now);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new ApiTokenValidation(
            token.UserId,
            token.Id,
            token.Scopes.Values.Select(s => s.ToWire()).ToList()));
    }

    public async Task<Result> RevokeAsync(ApiTokenId id, string? reason, CancellationToken ct)
    {
        var token = await repository.GetByIdAsync(id, ct);
        if (token is null)
        {
            return Result.Failure(DomainError.NotFound(
                "security.api_token.not_found", "API token was not found."));
        }

        var result = token.Revoke(by: Guid.Empty, reason, clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<ApiTokenSummary>> ListForUserAsync(UserId userId, CancellationToken ct)
    {
        var rows = await repository.ListForUserAsync(userId.Value, ct);
        return rows
            .Select(t => new ApiTokenSummary(
                t.Id.Value,
                t.Name.Value,
                t.SecretPrefix,
                t.Scopes.Values.Select(s => s.ToWire()).ToList(),
                t.CreatedAt,
                t.ExpiresAt,
                t.LastUsedAt,
                t.RevokedAt))
            .ToList();
    }

    private static (string cleartext, string hashed, string prefix) GenerateSecret()
    {
        Span<byte> bytes = stackalloc byte[ApiToken.SecretByteLength];
        RandomNumberGenerator.Fill(bytes);
        var cleartext = Base64UrlEncode(bytes);
        var hashed = HashSecret(cleartext);
        var prefix = cleartext[..Math.Min(ApiToken.SecretPrefixLength, cleartext.Length)];
        return (cleartext, hashed, prefix);
    }

    private static string HashSecret(string cleartext)
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
