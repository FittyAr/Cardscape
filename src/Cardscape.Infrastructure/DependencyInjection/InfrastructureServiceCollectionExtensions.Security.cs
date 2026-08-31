using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Infrastructure.Authentication;
using Cardscape.Infrastructure.Configuration;
using Cardscape.Infrastructure.Repositories;
using Cardscape.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Cardscape.Infrastructure.DependencyInjection;

public static partial class InfrastructureServiceCollectionExtensions
{
    private static void AddSecurityInfrastructure(
        IServiceCollection services,
        IConfiguration configuration)
    {
        InfrastructureOptions options = InfrastructureOptions.Bind(configuration);
        bool pendingTotpUsesRedis = options.PendingTotpStore.Backend == DistributedBackend.Redis;
        bool rateLimiterUsesRedis = options.RateLimiter.Backend == DistributedBackend.Redis;

        if (pendingTotpUsesRedis || rateLimiterUsesRedis)
        {
            if (string.IsNullOrWhiteSpace(options.Redis.ConnectionString))
            {
                throw new InvalidOperationException(
                    "Cardscape:Infrastructure:Redis:ConnectionString is required when at least "
                    + "one subsystem sets its Backend to 'Redis'. Check "
                    + "Cardscape:Infrastructure:RateLimiter:Backend and "
                    + "Cardscape:Infrastructure:PendingTotpStore:Backend.");
            }

            services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
            {
                IConfiguration currentConfiguration =
                    serviceProvider.GetRequiredService<IConfiguration>();
                string connectionString = currentConfiguration[
                    "Cardscape:Infrastructure:Redis:ConnectionString"]
                    ?? throw new InvalidOperationException(
                        "Cardscape:Infrastructure:Redis:ConnectionString is required.");
                ConfigurationOptions redisOptions = ConfigurationOptions.Parse(connectionString);
                redisOptions.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(redisOptions);
            });
        }

        if (pendingTotpUsesRedis)
        {
            services.AddSingleton<IPendingTotpLoginStore, RedisPendingTotpLoginStore>();
        }
        else
        {
            services.AddSingleton<IPendingTotpLoginStore, InMemoryPendingTotpLoginStore>();
        }

        services.AddDataProtection();
        services.AddSingleton<IDataProtector>(serviceProvider =>
        {
            var provider = serviceProvider.GetRequiredService<IDataProtectionProvider>();
            return provider.CreateProtector("Cardscape.Secrets.v1");
        });
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IApiTokenService, ApiTokenService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IRevokedTokenRepository, RevokedTokenRepository>();

        services.Configure<InfrastructureOptions>(
            configuration.GetSection(InfrastructureOptions.SectionName));
        if (rateLimiterUsesRedis)
        {
            services.AddSingleton<IRateLimiter, RedisRateLimiter>();
        }
        else
        {
            services.AddSingleton<IRateLimiter, RateLimiter>();
        }

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection("Jwt"))
            .Validate(
                jwt => !string.IsNullOrWhiteSpace(jwt.Issuer)
                    && !string.IsNullOrWhiteSpace(jwt.Audience)
                    && jwt.AccessTokenMinutes is >= 5 and <= 1_440,
                "JWT requires non-empty issuer/audience and an access-token lifetime between 5 minutes and 24 hours.")
            .ValidateOnStart();

        services.AddScoped<IOAuthAppRepository, OAuthAppRepository>();
        services.AddScoped<IOAuthAuthorizationCodeRepository, OAuthAuthorizationCodeRepository>();
        services.AddScoped<IOAuthAccessTokenRepository, OAuthAccessTokenRepository>();
        services.AddScoped<IOAuthAppService, OAuthAppService>();
    }
}
