namespace Cardscape.Application.Authentication.DTOs;

/// <summary>Payload for registering a new user.</summary>
public sealed record RegisterRequest(string Email, string DisplayName, string Password);

/// <summary>Payload for signing in.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Successful authentication response.</summary>
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    UserSummary User);

/// <summary>Compact user projection returned alongside auth responses.</summary>
public sealed record UserSummary(Guid Id, string Email, string DisplayName);
