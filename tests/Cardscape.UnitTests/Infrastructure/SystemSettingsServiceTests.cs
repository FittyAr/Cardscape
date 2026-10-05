using System.Net.Http;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Infrastructure.Settings;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cardscape.UnitTests.Infrastructure;

public sealed class SystemSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public SystemSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cardscape-settings-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var configValues = new Dictionary<string, string?>
        {
            ["Cardscape:DataRoot"] = _tempDir,
            ["Database:Provider"] = "PostgreSQL",
            ["ASPNETCORE_ENVIRONMENT"] = "Testing",
            ["Storage:LocalRoot"] = "TestStorage"
        };

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddHttpClient();
        ServiceProvider sp = services.BuildServiceProvider();
        _httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsDefaultConfiguredValues()
    {
        using var service = new SystemSettingsService(_config, NullLogger<SystemSettingsService>.Instance, _httpClientFactory);

        SystemSettingsDto settings = await service.GetSettingsAsync(TestContext.Current.CancellationToken);

        settings.InstanceTitle.Should().Be("Cardscape");
        settings.AllowPublicRegistration.Should().BeTrue();
        settings.DatabaseProvider.Should().Be("PostgreSQL");
        settings.Environment.Should().Be("Testing");
        settings.JwtAccessTokenMinutes.Should().Be(1440);
        settings.MaxAttachmentSizeMb.Should().Be(25);
        settings.AiEnabled.Should().BeFalse();
        settings.EmailNotificationsEnabled.Should().BeFalse();
        settings.WebhooksEnabled.Should().BeTrue();
        settings.RateLimitingEnabled.Should().BeTrue();
        settings.RetentionSweeperEnabled.Should().BeTrue();
        settings.DatabaseHealth.Should().Be("Healthy");
        settings.ActiveThreads.Should().BeGreaterThan(0);
        settings.CardAgingEnabled.Should().BeTrue();
        settings.CardAgingInactiveDays.Should().Be(14);
        settings.InboundEmailEnabled.Should().BeFalse();
        settings.MaxWorkspacesPerUser.Should().Be(0);
        settings.MaxBoardsPerWorkspace.Should().Be(0);
        settings.McpServerEnabled.Should().BeTrue();
        settings.EnableKeyboardShortcuts.Should().BeTrue();
        settings.EnforceHttps.Should().BeTrue();
        settings.SamlSsoEnabled.Should().BeFalse();
        settings.ScimProvisioningEnabled.Should().BeFalse();
        settings.OAuthAppsEnabled.Should().BeTrue();
        settings.CardRecurrenceEnabled.Should().BeTrue();
        settings.AllowCommentEditing.Should().BeTrue();
        settings.CustomFieldsEnabled.Should().BeTrue();
        settings.BoardAutomationEnabled.Should().BeTrue();
        settings.InAppNotificationsEnabled.Should().BeTrue();
        settings.DueSoonThresholdHours.Should().Be(24);
        settings.SearchFuzzyMatching.Should().BeTrue();
        settings.RealtimeBroadcastingEnabled.Should().BeTrue();
        settings.CacheAdminClaim.Should().BeTrue();
        settings.TotpIssuerName.Should().Be("Cardscape");
        settings.EnableIdempotency.Should().BeTrue();
        settings.EnableDashboards.Should().BeTrue();
        settings.EnableBoardExport.Should().BeTrue();
        settings.McpServerName.Should().Be("Cardscape-MCP");
        settings.RedisDatabase.Should().Be(0);
        settings.DeploymentRegion.Should().Be("Unspecified");
        settings.EnableActivityLogging.Should().BeTrue();
        settings.LogRetainedFileCountLimit.Should().Be(30);
    }

    [Fact]
    public async Task UpdateSettingsAsync_PersistsAndReflectsUpdatedValues()
    {
        using var service = new SystemSettingsService(_config, NullLogger<SystemSettingsService>.Instance, _httpClientFactory);

        var updateReq = new UpdateSystemSettingsRequest(
            InstanceTitle: "Empresa XYZ",
            SupportEmail: "ops@empresa.com",
            DefaultLanguage: "es",
            DefaultTheme: "dark",
            AllowPublicRegistration: false,
            JwtAccessTokenMinutes: 120,
            PasswordMinLength: 10,
            PasswordRequireDigit: true,
            PasswordRequireNonAlphanumeric: true,
            RequireTwoFactorForAdmins: true,
            MaxFailedLoginAttempts: 3,
            MaxAttachmentSizeMb: 50,
            AllowedAttachmentExtensions: "png,jpg,pdf",
            AiEnabled: true,
            AiProvider: "OpenAiCompatible",
            AiEndpoint: "http://localhost:11434/",
            AiModel: "mistral",
            AiApiKey: "test-secret-key",
            AiTimeoutSeconds: 45,
            EmailNotificationsEnabled: true,
            SmtpHost: "smtp.mail.com",
            SmtpPort: 465,
            SmtpUsername: "user",
            SmtpPassword: "password123",
            SmtpEnableSsl: true,
            SenderEmail: "bot@empresa.com",
            SenderName: "Bot",
            WebhooksEnabled: true,
            MaxWebhookRetries: 5,
            WebhookTimeoutSeconds: 15,
            SlackIntegrationEnabled: true,
            GitHubIntegrationEnabled: true,
            GoogleCalendarIntegrationEnabled: true,
            RateLimitingEnabled: true,
            DefaultRequestsPerHour: 5000,
            RateLimiterBackend: "Redis",
            RetentionSweeperEnabled: true,
            SweepIntervalHours: 12,
            UserGracePeriodDays: 14,
            ActivityRetentionDays: 180,
            AuditRetentionDays: 365,
            DataResidencyEnabled: true,
            SeederEnabled: false,
            CardAgingEnabled: true,
            CardAgingInactiveDays: 5,
            MaxWorkspacesPerUser: 25,
            SamlSsoEnabled: true,
            CustomFieldsEnabled: false,
            DueSoonThresholdHours: 48,
            RedisConnectionString: "redis-cache:6379",
            EnableGoogleAuth: true,
            GoogleClientId: "google-client-id",
            GoogleClientSecret: "google-secret-val",
            DeploymentRegion: "Europe",
            EnableIdempotency: false,
            MaxDashcardsPerBoard: 15,
            McpServerName: "MyCustomMCP");

        SystemSettingsDto updated = await service.UpdateSettingsAsync(updateReq, "admin@test.com", TestContext.Current.CancellationToken);

        updated.InstanceTitle.Should().Be("Empresa XYZ");
        updated.SupportEmail.Should().Be("ops@empresa.com");
        updated.DefaultLanguage.Should().Be("es");
        updated.DefaultTheme.Should().Be("dark");
        updated.AllowPublicRegistration.Should().BeFalse();
        updated.JwtAccessTokenMinutes.Should().Be(120);
        updated.PasswordMinLength.Should().Be(10);
        updated.RequireTwoFactorForAdmins.Should().BeTrue();
        updated.MaxAttachmentSizeMb.Should().Be(50);
        updated.AiApiKeyMasked.Should().Be("******");
        updated.SmtpPasswordMasked.Should().Be("******");
        updated.RateLimiterBackend.Should().Be("Redis");
        updated.DataResidencyEnabled.Should().BeTrue();
        updated.CardAgingEnabled.Should().BeTrue();
        updated.CardAgingInactiveDays.Should().Be(5);
        updated.MaxWorkspacesPerUser.Should().Be(25);
        updated.SamlSsoEnabled.Should().BeTrue();
        updated.CustomFieldsEnabled.Should().BeFalse();
        updated.DueSoonThresholdHours.Should().Be(48);
        updated.RedisConnectionStringMasked.Should().Be("******");
        updated.EnableGoogleAuth.Should().BeTrue();
        updated.GoogleClientId.Should().Be("google-client-id");
        updated.GoogleClientSecretMasked.Should().Be("******");
        updated.DeploymentRegion.Should().Be("Europe");
        updated.EnableIdempotency.Should().BeFalse();
        updated.MaxDashcardsPerBoard.Should().Be(15);
        updated.McpServerName.Should().Be("MyCustomMCP");

        bool isAllowed = await service.IsPublicRegistrationAllowedAsync(TestContext.Current.CancellationToken);
        isAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task ResetToDefaultsAsync_RestoresDefaultSettings()
    {
        using var service = new SystemSettingsService(_config, NullLogger<SystemSettingsService>.Instance, _httpClientFactory);

        var updateReq = new UpdateSystemSettingsRequest(
            InstanceTitle: "Nombre Modificado",
            AllowPublicRegistration: false);

        await service.UpdateSettingsAsync(updateReq, "admin@test.com", TestContext.Current.CancellationToken);

        SystemSettingsDto reset = await service.ResetToDefaultsAsync("admin@test.com", TestContext.Current.CancellationToken);

        reset.InstanceTitle.Should().Be("Cardscape");
        reset.AllowPublicRegistration.Should().BeTrue();
        reset.JwtAccessTokenMinutes.Should().Be(1440);
    }

    [Fact]
    public async Task TestAiConnectionAsync_WhenAiDisabled_ReturnsFailure()
    {
        using var service = new SystemSettingsService(_config, NullLogger<SystemSettingsService>.Instance, _httpClientFactory);

        TestAiResponse res = await service.TestAiConnectionAsync(TestContext.Current.CancellationToken);

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("no está habilitado");
    }

    [Fact]
    public async Task TestEmailAsync_WhenEmailDisabled_ReturnsFailure()
    {
        using var service = new SystemSettingsService(_config, NullLogger<SystemSettingsService>.Instance, _httpClientFactory);

        TestEmailResponse res = await service.TestEmailAsync("test@domain.com", TestContext.Current.CancellationToken);

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("no está habilitado");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
