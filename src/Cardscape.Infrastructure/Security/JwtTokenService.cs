using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Members;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Cardscape.Infrastructure.Security;

/// <summary>Configuration for the JWT token service.</summary>
public sealed class JwtOptions
{
    public string Issuer { get; set; } = "Cardscape";
    public string Audience { get; set; } = "Cardscape";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 30;
}

public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    IClock clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public string IssueAccessToken(User user, IReadOnlyCollection<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new("display_name", user.DisplayName.Value),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // Cached admin status. The dedicated admin policies
            // (AdminOnlyPolicy + the McpSubscriptionsAdminPolicy
            // variant) read this claim instead of hitting the
            // users table on every request. The claim reflects
            // the IsAdmin value at token-mint time; a fresh
            // login is required to pick up a status change.
            new("is_admin", user.IsAdmin ? "true" : "false")
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: clock.UtcNow.UtcDateTime,
            expires: clock.UtcNow.AddMinutes(_options.AccessTokenMinutes).UtcDateTime,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshToken IssueRefreshToken()
    {
        var random = RandomNumberGenerator.GetBytes(48);
        var token = Convert.ToBase64String(random);
        return new RefreshToken(token, clock.UtcNow.AddDays(_options.RefreshTokenDays));
    }

    public Guid? GetUserIdFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var sub = jwt.Subject;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }
}
