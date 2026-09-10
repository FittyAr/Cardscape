using System.Text;
using Cardscape.Api.Authentication;
using Cardscape.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Cardscape.Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    private static AuthenticationBuilder AddCoreAuthentication(
        IServiceCollection services,
        IConfiguration configuration,
        string signingKey)
    {
        AuthenticationBuilder authentication = services.AddAuthentication(options =>
        {
            options.DefaultScheme = BearerPolicyScheme;
            options.DefaultAuthenticateScheme = BearerPolicyScheme;
            options.DefaultChallengeScheme = BearerPolicyScheme;
            options.DefaultSignInScheme = ExternalCookieScheme;
        });

        authentication.AddCookie(ExternalCookieScheme, options =>
        {
            options.Cookie.Name = "cardscape.external";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            options.SlidingExpiration = false;
        });

        authentication.AddPolicyScheme(
            BearerPolicyScheme,
            "Bearer policy (JWT or API token)",
            options => options.ForwardDefaultSelector = SelectBearerScheme);

        authentication.AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "Cardscape",
                ValidAudience = configuration["Jwt:Audience"] ?? "Cardscape",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ResolveRequestTokenAsync,
                OnTokenValidated = context =>
                    context.HttpContext.RequestServices
                        .GetRequiredService<JwtRevocationValidator>()
                        .OnTokenValidatedAsync(context)
            };
        });

        services.AddSingleton<JwtRevocationValidator>();
        authentication.AddScheme<ApiTokenAuthenticationOptions, ApiTokenAuthenticationHandler>(
            ApiTokenAuthenticationHandler.SchemeName,
            _ => { });

        return authentication;
    }

    private static string SelectBearerScheme(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments(
            "/scim/v2",
            StringComparison.OrdinalIgnoreCase))
        {
            return ScimAuthenticationHandler.SchemeName;
        }

        if (!context.Request.Headers.TryGetValue("Authorization", out var authorization))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }

        string raw = authorization.ToString();
        if (!raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }

        string secret = raw["Bearer ".Length..].Trim();
        return !string.IsNullOrEmpty(secret) && !secret.Contains('.')
            ? ApiTokenAuthenticationHandler.SchemeName
            : JwtBearerDefaults.AuthenticationScheme;
    }

    private static Task ResolveRequestTokenAsync(MessageReceivedContext context)
    {
        if (context.Request.Cookies.TryGetValue("Cardscape.AccessToken", out string? cookieToken)
            && !string.IsNullOrWhiteSpace(cookieToken))
        {
            context.Token = cookieToken;
            return Task.CompletedTask;
        }

        string accessToken = context.Request.Query["access_token"].ToString();
        if (!string.IsNullOrEmpty(accessToken)
            && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
        {
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }
}
