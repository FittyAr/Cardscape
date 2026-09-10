namespace Cardscape.Web.Shared;

// ── API tokens (long-lived; for the MCP server) ───────────
public sealed record ApiTokenSummaryDto(
    Guid Id,
    string Name,
    string SecretPrefix,
    IReadOnlyCollection<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    int RateLimitPerHour,
    int BurstSize);

public sealed record ApiTokenIssuanceDto(Guid Id, string CleartextSecret);

public sealed record IssueApiTokenRequestDto(
    string Name,
    IReadOnlyCollection<string> Scopes,
    DateTimeOffset? ExpiresAt,
    int? RateLimitPerHour = null,
    int? BurstSize = null);

public sealed record ApiTokenRateLimitStatusDto(
    Guid TokenId,
    int RateLimitPerHour,
    int BurstSize,
    double AvailableTokens,
    DateTimeOffset At);

public sealed record UpdateApiTokenRateLimitRequestDto(int RateLimitPerHour, int BurstSize);
