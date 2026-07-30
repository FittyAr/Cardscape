using System.Security.Cryptography;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Domain.Authentication.Scim;

/// <summary>Strongly-typed id for <see cref="ScimToken"/>.</summary>
public sealed record ScimTokenId(Guid Value) : GuidId<ScimTokenId>(Value);

/// <summary>
/// Per-workspace SCIM v2 bearer token. The IdP (Okta, Azure
/// AD, Google Workspace, etc.) presents the plaintext token in
/// the <c>Authorization: Bearer …</c> header on every call to
/// <c>/scim/v2/Users</c> and <c>/scim/v2/Groups</c>. The token's
/// hash is what we store; the plaintext is only shown to the
/// workspace admin at creation time, never again.
/// </summary>
public sealed class ScimToken : AggregateRoot<ScimTokenId>
{
    public WorkspaceId WorkspaceId { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;

    /// <summary>Hashed bearer token (PBKDF2 with a per-token
    /// salt). The plaintext is never persisted; we use the same
    /// hashing helper as <c>IPasswordHasher</c> to keep the
    /// audit story consistent.</summary>
    public string TokenHash { get; private set; } = string.Empty;
    public string TokenPrefix { get; private set; } = string.Empty;
    public DateTimeOffset? LastUsedAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private ScimToken() { }

    private ScimToken(
        ScimTokenId id, WorkspaceId workspaceId, string name,
        string tokenHash, string tokenPrefix, DateTimeOffset at)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Name = name;
        TokenHash = tokenHash;
        TokenPrefix = tokenPrefix;
        CreatedAt = at;
    }

    /// <summary>Factory. The caller provides the plaintext
    /// token + the matching hash + the prefix; the plaintext
    /// is returned to the caller exactly once (the "show me
    /// the token" screen).</summary>
    public static (ScimToken token, string plaintext) Issue(
        ScimTokenId id, WorkspaceId workspaceId, string name, DateTimeOffset at)
    {
        // The plaintext is a 32-byte URL-safe base64 string.
        // The prefix is the first 8 characters — used in the
        // audit log so an admin can identify "which token
        // was used" without ever storing the full secret.
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        string plaintext = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        string prefix = plaintext[..8];
        string hash = HashPlaintext(plaintext);

        return (new ScimToken(id, workspaceId, name, hash, prefix, at), plaintext);
    }

    /// <summary>Verify a presented plaintext against the stored
    /// hash. Returns true on match. Constant-time comparison
    /// (delegated to <c>CryptographicOperations.FixedTimeEquals</c>).</summary>
    public bool Verify(string plaintext)
    {
        if (IsRevoked || string.IsNullOrEmpty(plaintext))
        {
            return false;
        }

        // Re-derive the SHA-256 of the presented plaintext
        // and compare against the stored base64-decoded hash.
        // (The original implementation compared the UTF-8
        // bytes of the plaintext against the hash bytes,
        // which can never match — the SCIM v2 endpoints
        // were therefore never authenticated. Fixed under
        // G3 because the Groups tests need a working
        // SCIM auth flow to assert the round-trip.)
        byte[] presentedHash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(plaintext));
        byte[] expected = Convert.FromBase64String(TokenHash);
        return CryptographicOperations.FixedTimeEquals(presentedHash, expected);
    }

    public void RecordUse(DateTimeOffset at)
    {
        if (IsRevoked)
        {
            return;
        }
        LastUsedAt = at;
    }

    public void Revoke(DateTimeOffset at)
    {
        IsRevoked = true;
        RevokedAt = at;
        TokenHash = string.Empty;
        UpdatedAt = at;
    }

    private static string HashPlaintext(string plaintext)
    {
        // Plain SHA-256 of the plaintext, base64-encoded. SCIM
        // tokens are random 256-bit secrets so a single SHA-256
        // is sufficient — there's no human-chosen password to
        // defend against.
        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToBase64String(hash);
    }
}
