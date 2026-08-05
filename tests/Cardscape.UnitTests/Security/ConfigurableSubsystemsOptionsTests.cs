using Cardscape.Api.Authentication;
using Cardscape.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace Cardscape.UnitTests.Security;

/// <summary>
/// Configuration-binding tests for the three operator-tunable
/// subsystems documented in
/// <c>docs/operations/06-configurable-subsystems.md</c>:
///
/// <list type="bullet">
///   <item><see cref="AdminAuthorizationOptions"/> (the
///         <c>is_admin</c> claim cache)</item>
///   <item><see cref="RateLimiterOptions"/> (InMemory vs Redis
///         backend)</item>
///   <item><see cref="PendingTotpStoreOptions"/> (InMemory vs
///         Redis backend)</item>
/// </list>
///
/// The tests pin the defaults, the override behaviour, and the
/// error posture (a misconfigured value should fail loud at
/// composition time, not silently).
/// </summary>
public class ConfigurableSubsystemsOptionsTests
{
    [Fact]
    public void AdminAuthorizationOptions_Default_IsCacheEnabled()
    {
        AdminAuthorizationOptions options = new();

        options.CacheAdminClaim.Should().BeTrue(
            "the default posture must be the cheap, cached path; " +
            "the strict DB-lookup path is opt-in.");
    }

    [Fact]
    public void AdminAuthorizationOptions_BindsFromConfiguration()
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cardscape:Api:AdminAuthorization:CacheAdminClaim"] = "false"
            })
            .Build();

        AdminAuthorizationOptions options = new();
        config.GetSection(AdminAuthorizationOptions.SectionName).Bind(options);

        options.CacheAdminClaim.Should().BeFalse();
    }

    [Fact]
    public void AdminAuthorizationOptions_BindsEnvironmentVariableStyle()
    {
        // Operators override the JSON file with environment
        // variables; the standard .NET configuration provider
        // converts Cardscape:Api:AdminAuthorization:CacheAdminClaim
        // into the env-var key
        // Cardscape__Api__AdminAuthorization__CacheAdminClaim.
        // We use the real env-var provider here (not
        // AddInMemoryCollection, which does NOT do the
        // __ → : translation) so the test reflects the
        // production path.
        const string key = "Cardscape__Api__AdminAuthorization__CacheAdminClaim";
        Environment.SetEnvironmentVariable(key, "false");
        try
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            AdminAuthorizationOptions options = new();
            config.GetSection(AdminAuthorizationOptions.SectionName).Bind(options);

            options.CacheAdminClaim.Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void InfrastructureOptions_Defaults_AreInMemoryEverywhere()
    {
        InfrastructureOptions options = new();

        options.RateLimiter.Backend.Should().Be(DistributedBackend.InMemory);
        options.PendingTotpStore.Backend.Should().Be(DistributedBackend.InMemory);
        options.Redis.Database.Should().Be(0);
        options.RateLimiter.KeyPrefix.Should().NotBeNullOrWhiteSpace();
        options.PendingTotpStore.KeyPrefix.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void InfrastructureOptions_BindsRedisBackendFromConfiguration()
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cardscape:Infrastructure:Redis:ConnectionString"] = "redis-host:6379,abortConnect=false",
                ["Cardscape:Infrastructure:Redis:Database"] = "3",
                ["Cardscape:Infrastructure:RateLimiter:Backend"] = "Redis",
                ["Cardscape:Infrastructure:RateLimiter:KeyPrefix"] = "tenant-a:rl:",
                ["Cardscape:Infrastructure:PendingTotpStore:Backend"] = "Redis",
                ["Cardscape:Infrastructure:PendingTotpStore:KeyPrefix"] = "tenant-a:totp:"
            })
            .Build();

        InfrastructureOptions options = InfrastructureOptions.Bind(config);

        options.Redis.ConnectionString.Should().Be("redis-host:6379,abortConnect=false");
        options.Redis.Database.Should().Be(3);
        options.RateLimiter.Backend.Should().Be(DistributedBackend.Redis);
        options.RateLimiter.KeyPrefix.Should().Be("tenant-a:rl:");
        options.PendingTotpStore.Backend.Should().Be(DistributedBackend.Redis);
        options.PendingTotpStore.KeyPrefix.Should().Be("tenant-a:totp:");
    }

    [Fact]
    public void InfrastructureOptions_AcceptsCaseInsensitiveBackendNames()
    {
        // Operators write "redis" in their .env file half the
        // time; the binder must not reject it.
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cardscape:Infrastructure:RateLimiter:Backend"] = "redis",
                ["Cardscape:Infrastructure:PendingTotpStore:Backend"] = "INMEMORY"
            })
            .Build();

        InfrastructureOptions options = InfrastructureOptions.Bind(config);

        options.RateLimiter.Backend.Should().Be(DistributedBackend.Redis);
        options.PendingTotpStore.Backend.Should().Be(DistributedBackend.InMemory);
    }

    [Fact]
    public void InfrastructureOptions_BackendDefaultsToInMemoryWhenSectionMissing()
    {
        // An operator who deletes the entire Infrastructure
        // section (or runs a build that ships the old
        // appsettings without the new keys) should land on
        // the safe InMemory defaults, not crash.
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        InfrastructureOptions options = InfrastructureOptions.Bind(config);

        options.RateLimiter.Backend.Should().Be(DistributedBackend.InMemory);
        options.PendingTotpStore.Backend.Should().Be(DistributedBackend.InMemory);
    }
}
