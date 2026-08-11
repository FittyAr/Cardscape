namespace Cardscape.Application.Authentication.DTOs;

/// <summary>Payload for registering a new user.</summary>
public sealed record RegisterRequest(string Email, string DisplayName, string Password);

/// <summary>Payload for signing in.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Payload for the TOTP second step of a 2FA-protected login.</summary>
public sealed record LoginWithTotpRequest(string PendingTotpToken, string Code);

/// <summary>
/// Authentication response. The access token is <c>null</c> on the
/// 2FA challenge path (when <see cref="RequiresTotp"/> is <c>true</c>):
/// the caller must POST the <see cref="PendingTotpToken"/> + a 6-digit
/// code to <c>/api/auth/login/totp</c> to obtain the JWT.
/// </summary>
public sealed record AuthResponse(
    string? AccessToken,
    DateTimeOffset? AccessTokenExpiresAt,
    UserSummary User,
    bool RequiresTotp = false,
    string? PendingTotpToken = null);

/// <summary>Compact user projection returned alongside auth responses.</summary>
public sealed record UserSummary(Guid Id, string Email, string DisplayName);
