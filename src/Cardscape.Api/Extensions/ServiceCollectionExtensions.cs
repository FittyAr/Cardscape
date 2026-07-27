using System.Text;
using Cardscape.Api.Authentication;
using Cardscape.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Cardscape.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Wires the JWT bearer authentication scheme. The signing key
    /// comes from <c>Jwt:SigningKey</c> in configuration. In
    /// development a stable default is used so the smoke tests
    /// have a known secret; in production the key MUST be
    /// overridden by configuration.
    /// </summary>
    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string signingKey = configuration["Jwt:SigningKey"]
            ?? "dev-only-insecure-signing-key-please-override-in-production-32+chars";

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "Cardscape",
                ValidAudience = configuration["Jwt:Audience"] ?? "Cardscape",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            });

        services.AddAuthorization();
        return services;
    }
}
