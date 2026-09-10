using System.Text;
using Cardscape.Api.Authentication;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Cardscape.Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    private static void AddSamlMetadataClient(IServiceCollection services)
    {
        services.AddHttpClient(SamlAuthenticationHandler.MetadataHttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
    }

    private static string ResolveJwtSigningKey(IConfiguration configuration)
    {
        string? signingKey = configuration["Jwt:SigningKey"];
        bool isDevelopment = IsDevelopment(configuration);

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            if (isDevelopment)
            {
                return "dev-only-insecure-signing-key-please-override-in-production-32+chars";
            }

            throw new InvalidOperationException(
                "Jwt:SigningKey is required outside the Development environment. "
                + "Configure it via appsettings, environment variables, or a "
                + "secret store before starting the API.");
        }

        int signingKeyBytes = Encoding.UTF8.GetByteCount(signingKey);
        if (signingKeyBytes < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be at least 32 bytes (256 bits) for HS256. "
                + $"Current length: {signingKeyBytes} bytes.");
        }

        return signingKey;
    }

    private static void AddApiCors(IServiceCollection services, IConfiguration configuration)
    {
        string[]? configuredOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>();

        string[] allowedOrigins = configuredOrigins
            ?? (IsDevelopment(configuration)
                ? ["http://localhost:5206", "https://localhost:7188"]
                : throw new InvalidOperationException(
                    "Cors:AllowedOrigins is required outside the Development environment. "
                    + "Configure the list of origins allowed to call this API with credentials."));

        if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins is empty. List at least one origin or remove the section entirely.");
        }

        services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));
    }

    private static void AddApiAuthorization(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthorization(options =>
        {
            AddAdminPolicy(options, AdminOnlyPolicy.Name);
            AddAdminPolicy(options, McpSubscriptionsAdminPolicy.Name);
        });
        services.Configure<AdminAuthorizationOptions>(
            configuration.GetSection(AdminAuthorizationOptions.SectionName));
        services.AddScoped<IAuthorizationHandler, AdminOnlyAuthorizationHandler>();
    }

    private static void AddAdminPolicy(AuthorizationOptions options, string policyName) =>
        options.AddPolicy(
            policyName,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new AdminOnlyRequirement()));

    private static bool IsDevelopment(IConfiguration configuration) =>
        string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"]
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? "Production",
            "Development",
            StringComparison.OrdinalIgnoreCase);
}

/// <summary>Name of the policy that restricts an endpoint to administrators.</summary>
public static class AdminOnlyPolicy
{
    public const string Name = "AdminOnly";
}
