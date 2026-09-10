namespace Cardscape.Web.Shared;

// ── Auth ────────────────────────────────────────────────
public sealed record RegisterRequestDto(string Email, string DisplayName, string Password);

public sealed record LoginRequestDto(string Email, string Password);

/// <summary>Second-step body for a 2FA-protected login.</summary>
public sealed record LoginWithTotpRequestDto(string PendingTotpToken, string Code);

public sealed record UserSummaryDto(Guid Id, string Email, string DisplayName);

/// <summary>
/// Mirrors the server-side <c>AuthResponse</c>. The access token is
/// <c>null</c> when <see cref="RequiresTotp"/> is <c>true</c>:
/// the caller must POST the <see cref="PendingTotpToken"/> + a
/// 6-digit code to <c>api/auth/login/totp</c>.
/// </summary>
public sealed record AuthResponseDto(
    string? AccessToken,
    UserSummaryDto User,
    bool RequiresTotp = false,
    string? PendingTotpToken = null);
