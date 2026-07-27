using System.Security.Claims;
using System.Text.Json;
using Cardscape.Web.Shared;
using Microsoft.AspNetCore.Components.Authorization;

namespace Cardscape.Web.Services;

/// <summary>
/// Wires the Blazor <see cref="AuthenticationStateProvider"/> to
/// <see cref="TokenStore"/>. The token itself is the source of
/// truth for "is the user logged in?" — we decode the JWT payload
/// to get the user id, name, and email claims.
/// </summary>
public sealed class AuthStateProvider(TokenStore tokens) : AuthenticationStateProvider
{
    public const string AuthenticationType = "Cardscape";

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        string? accessToken = await tokens.GetAccessTokenAsync();
        UserSummaryDto? user = await tokens.GetUserAsync();

        if (string.IsNullOrWhiteSpace(accessToken) || user is null)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        ClaimsIdentity identity = BuildIdentity(user, accessToken);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>Call this after login or logout to make the UI react.</summary>
    public void Notify() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static ClaimsIdentity BuildIdentity(UserSummaryDto user, string accessToken)
    {
        List<Claim> claims = DecodeJwtClaims(accessToken);
        claims.Add(new(ClaimTypes.NameIdentifier, user.Id.ToString()));
        claims.Add(new(ClaimTypes.Email, user.Email));
        claims.Add(new(ClaimTypes.Name, user.DisplayName));
        claims.Add(new("display_name", user.DisplayName));
        return new ClaimsIdentity(claims, AuthenticationType, ClaimTypes.Name, ClaimTypes.Role);
    }

    private static List<Claim> DecodeJwtClaims(string token)
    {
        // We do not validate the signature here — the API does that on every request.
        // This is purely so the UI can show the user's id/name without a roundtrip.
        try
        {
            string[] parts = token.Split('.');
            if (parts.Length != 3)
            {
                return [];
            }

            string payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            byte[] bytes = Convert.FromBase64String(payload);
            using JsonDocument doc = JsonDocument.Parse(bytes);
            JsonElement root = doc.RootElement;
            List<Claim> claims = [];
            if (root.TryGetProperty("sub", out JsonElement sub))
            {
                claims.Add(new(ClaimTypes.NameIdentifier, sub.GetString() ?? string.Empty));
            }

            if (root.TryGetProperty("email", out JsonElement email))
            {
                claims.Add(new(ClaimTypes.Email, email.GetString() ?? string.Empty));
            }

            if (root.TryGetProperty("name", out JsonElement name))
            {
                claims.Add(new(ClaimTypes.Name, name.GetString() ?? string.Empty));
            }

            if (root.TryGetProperty("role", out JsonElement role))
            {
                if (role.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement r in role.EnumerateArray())
                    {
                        claims.Add(new(ClaimTypes.Role, r.GetString() ?? string.Empty));
                    }
                }
                else
                {
                    claims.Add(new(ClaimTypes.Role, role.GetString() ?? string.Empty));
                }
            }

            return claims;
        }
        catch
        {
            return [];
        }
    }
}
