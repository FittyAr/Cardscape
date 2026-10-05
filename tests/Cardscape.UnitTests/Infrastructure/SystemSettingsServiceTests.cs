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
        settings.CustomLogoUrl.Should().BeEmpty();
        settings.CustomFaviconUrl.Should().BeEmpty();
        settings.HidePoweredByCardscape.Should().BeFalse();
        settings.IdleSessionTimeoutMinutes.Should().Be(0);
        settings.MaxConcurrentSessionsPerUser.Should().Be(5);
        settings.PasswordExpiryDays.Should().Be(0);
        settings.PasswordHistoryCount.Should().Be(0);
        settings.EnablePublicBoardTemplates.Should().BeTrue();
        settings.AllowCustomUserTemplates.Should().BeTrue();
        settings.HighlightOverLimitLists.Should().BeTrue();
        settings.EnableTimelineView.Should().BeTrue();
        settings.EnableCalendarView.Should().BeTrue();
        settings.EnableTableView.Should().BeTrue();
        settings.DefaultBoardView.Should().Be("Kanban");
        settings.EnableTimeTracking.Should().BeFalse();
        settings.EnforceTimeTrackingEstimates.Should().BeFalse();
        settings.TimeTrackingUnit.Should().Be("Hours");
        settings.AutomationMonthlyRunQuotaPerUser.Should().Be(250);
        settings.AutomationTimeoutSeconds.Should().Be(15);
        settings.EnableWebPushNotifications.Should().BeFalse();
        settings.VapidSubject.Should().Be("mailto:admin@cardscape.local");
        settings.VapidPublicKey.Should().BeEmpty();
        settings.VapidPrivateKeyMasked.Should().BeEmpty();
        settings.AiEnableCardDescriptionGen.Should().BeTrue();
        settings.AiEnableCommentSummary.Should().BeTrue();
        settings.AiEnableAutoChecklists.Should().BeTrue();
        settings.AiTemperature.Should().Be(70);
        settings.GoogleDriveIntegrationEnabled.Should().BeFalse();
        settings.OneDriveIntegrationEnabled.Should().BeFalse();
        settings.DropboxIntegrationEnabled.Should().BeFalse();
        settings.MicrosoftTeamsIntegrationEnabled.Should().BeFalse();
        settings.MicrosoftTeamsWebhookUrlMasked.Should().BeEmpty();
        settings.DiscordIntegrationEnabled.Should().BeFalse();
        settings.DiscordWebhookUrlMasked.Should().BeEmpty();
        settings.GitLabIntegrationEnabled.Should().BeFalse();
        settings.GitLabEndpoint.Should().BeEmpty();
        settings.CustomPrivacyPolicyUrl.Should().BeEmpty();
        settings.CustomTermsOfServiceUrl.Should().BeEmpty();
        settings.DisplayCookieBanner.Should().BeFalse();
        settings.RequireLegalNoticeAcceptance.Should().BeFalse();
        settings.AutoBackupEnabled.Should().BeFalse();
        settings.BackupIntervalHours.Should().Be(24);
        settings.BackupRetentionDays.Should().Be(30);
        settings.MaintenanceModeEnabled.Should().BeFalse();
        settings.MaintenanceModeMessage.Should().Be("El sistema se encuentra en mantenimiento programado.");
        settings.SystemAnnouncementEnabled.Should().BeFalse();
        settings.SystemAnnouncementMessage.Should().BeEmpty();
        settings.SystemAnnouncementType.Should().Be("Info");
        settings.HstsMaxAgeSeconds.Should().Be(31536000);
        settings.HstsIncludeSubdomains.Should().BeTrue();
        settings.HstsPreload.Should().BeFalse();
        settings.ContentSecurityPolicy.Should().Contain("default-src");
        settings.XFrameOptions.Should().Be("DENY");
        settings.ReferrerPolicy.Should().Be("no-referrer");
        settings.DataProtectionKeyLifetimeDays.Should().Be(90);
        settings.DataProtectionKeyDirectory.Should().BeEmpty();
        settings.EnableAppleAuth.Should().BeFalse();
        settings.AppleClientId.Should().BeEmpty();
        settings.AppleTeamId.Should().BeEmpty();
        settings.AppleKeyId.Should().BeEmpty();
        settings.ApplePrivateKeyPemMasked.Should().BeEmpty();
        settings.S3BucketName.Should().BeEmpty();
        settings.S3EndpointUrl.Should().BeEmpty();
        settings.S3Region.Should().Be("us-east-1");
        settings.S3AccessKey.Should().BeEmpty();
        settings.S3SecretKeyMasked.Should().BeEmpty();
        settings.S3ForcePathStyle.Should().BeTrue();
        settings.BlockExecutableAttachments.Should().BeTrue();
        settings.ScanAttachmentsForMalware.Should().BeFalse();
        settings.ClamAvDaemonEndpoint.Should().BeEmpty();
        settings.SlackClientId.Should().BeEmpty();
        settings.SlackClientSecretMasked.Should().BeEmpty();
        settings.SlackSigningSecretMasked.Should().BeEmpty();
        settings.SlackBotTokenMasked.Should().BeEmpty();
        settings.GitHubTokenMasked.Should().BeEmpty();
        settings.GitHubSyncIntervalMinutes.Should().Be(15);
        settings.GitHubAutoCloseCardsOnPrMerge.Should().BeTrue();
        settings.GoogleCalendarClientId.Should().BeEmpty();
        settings.GoogleCalendarClientSecretMasked.Should().BeEmpty();
        settings.GoogleCalendarSyncIntervalMinutes.Should().Be(15);
        settings.OtelTracingEnabled.Should().BeFalse();
        settings.OtelMetricsEnabled.Should().BeFalse();
        settings.OtelEndpointUrl.Should().BeEmpty();
        settings.OtelServiceName.Should().Be("Cardscape.Api");
        settings.OtelTraceSampleRate.Should().Be(100);
        settings.OutboxProcessorEnabled.Should().BeTrue();
        settings.OutboxBatchSize.Should().Be(50);
        settings.OutboxPollIntervalSeconds.Should().Be(5);
        settings.OutboxMaxAttempts.Should().Be(5);
        settings.OutboxMessageRetentionDays.Should().Be(14);
        settings.SoftDeleteRetentionDays.Should().Be(30);
        settings.PermanentDeleteRequiresAdmin.Should().BeTrue();
        settings.TotpCodeLength.Should().Be(6);
        settings.TotpRecoveryCodesCount.Should().Be(10);
        settings.TotpRecoveryCodeLength.Should().Be(10);
        settings.JwtRefreshTokenDays.Should().Be(7);
        settings.JwtIssuer.Should().Be("Cardscape");
        settings.JwtAudience.Should().Be("CardscapeClient");
        settings.MaxWorkspaceNameLength.Should().Be(100);
        settings.MaxBoardNameLength.Should().Be(100);
        settings.MaxBoardDescriptionLength.Should().Be(2000);
        settings.MaxListNameLength.Should().Be(100);
        settings.MaxDisplayNameLength.Should().Be(80);
        settings.MaxCardTitleLength.Should().Be(500);
        settings.MaxCardDescriptionLength.Should().Be(16000);
        settings.MaxChecklistItemLength.Should().Be(500);
        settings.MaxAttachmentsPerCard.Should().Be(50);
        settings.AllowedAvatarExtensions.Should().Be("png,jpg,jpeg,webp");
        settings.MaxAvatarSizeMb.Should().Be(2);
        settings.EmailRateLimitPerMinute.Should().Be(60);
        settings.EmailBatchSize.Should().Be(20);
        settings.EmailIncludeUnsubscribeLink.Should().BeFalse();
        settings.DefaultPageSize.Should().Be(50);
        settings.MaxPageSize.Should().Be(200);
        settings.ActivityFeedPageSize.Should().Be(50);
        settings.ResponseCompressionEnabled.Should().BeTrue();
        settings.ResponseCompressionLevel.Should().Be("Optimal");
        settings.StaticFilesMaxAgeSeconds.Should().Be(86400);
        settings.ForwardedHeadersEnabled.Should().BeTrue();
        settings.MaxRequestBodySizeMb.Should().Be(30);
        settings.DatabaseCommandTimeoutSeconds.Should().Be(30);
        settings.DatabaseMaxRetryCount.Should().Be(3);
        settings.DatabaseMaxRetryDelaySeconds.Should().Be(5);
        settings.DatabaseEnableDetailedErrors.Should().BeFalse();
        settings.DatabaseEnableSensitiveDataLogging.Should().BeFalse();
        settings.RunMigrationsOnStartup.Should().BeTrue();
        settings.EnableSwaggerInProduction.Should().BeFalse();
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
            SlackClientId: "slack-id-123",
            SlackClientSecret: "slack-secret-xyz",
            SlackSigningSecret: "slack-sign-sec",
            SlackBotToken: "xoxb-test-token",
            GitHubIntegrationEnabled: true,
            GitHubToken: "ghp_my_secret_token",
            GitHubSyncIntervalMinutes: 30,
            GitHubAutoCloseCardsOnPrMerge: true,
            GoogleCalendarIntegrationEnabled: true,
            GoogleCalendarClientId: "gcal-client-id-val",
            GoogleCalendarClientSecret: "gcal-secret-val",
            GoogleCalendarSyncIntervalMinutes: 20,
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
            McpServerName: "MyCustomMCP",
            CustomLogoUrl: "/images/logo-custom.png",
            HidePoweredByCardscape: true,
            IdleSessionTimeoutMinutes: 30,
            PasswordExpiryDays: 90,
            DefaultBoardView: "Timeline",
            EnableTimeTracking: true,
            AutomationMonthlyRunQuotaPerUser: 5000,
            EnableWebPushNotifications: true,
            VapidPrivateKey: "vapid-secret-key",
            MicrosoftTeamsIntegrationEnabled: true,
            MicrosoftTeamsWebhookUrl: "https://teams.webhook.office.com/test",
            DiscordIntegrationEnabled: true,
            DiscordWebhookUrl: "https://discord.com/api/webhooks/test",
            GitLabIntegrationEnabled: true,
            GitLabEndpoint: "https://gitlab.myorg.internal",
            DisplayCookieBanner: true,
            AutoBackupEnabled: true,
            BackupIntervalHours: 12,
            MaintenanceModeEnabled: true,
            MaintenanceModeMessage: "Mantenimiento urgente",
            SystemAnnouncementEnabled: true,
            SystemAnnouncementMessage: "Aviso a todos los usuarios",
            SystemAnnouncementType: "Warning",
            HstsMaxAgeSeconds: 63072000,
            HstsIncludeSubdomains: true,
            HstsPreload: true,
            ContentSecurityPolicy: "default-src 'self';",
            XFrameOptions: "SAMEORIGIN",
            ReferrerPolicy: "strict-origin-when-cross-origin",
            DataProtectionKeyLifetimeDays: 180,
            DataProtectionKeyDirectory: "/keys/dir",
            EnableAppleAuth: true,
            AppleClientId: "com.apple.test",
            AppleTeamId: "TEAMXYZ",
            AppleKeyId: "KEYXYZ",
            ApplePrivateKeyPem: "-----BEGIN PRIVATE KEY-----\nMIGTAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBHkwdwIBAQQg...\n-----END PRIVATE KEY-----",
            S3BucketName: "my-bucket",
            S3EndpointUrl: "http://minio:9000",
            S3Region: "us-west-2",
            S3AccessKey: "S3ACCESS",
            S3SecretKey: "S3SECRET",
            S3ForcePathStyle: true,
            BlockExecutableAttachments: true,
            ScanAttachmentsForMalware: true,
            ClamAvDaemonEndpoint: "tcp://clamav:3310",
            OtelTracingEnabled: true,
            OtelMetricsEnabled: true,
            OtelEndpointUrl: "http://otel:4317",
            OtelServiceName: "Cardscape.Custom",
            OtelTraceSampleRate: 50,
            OutboxProcessorEnabled: true,
            OutboxBatchSize: 100,
            OutboxPollIntervalSeconds: 2,
            OutboxMaxAttempts: 10,
            OutboxMessageRetentionDays: 30,
            SoftDeleteRetentionDays: 60,
            PermanentDeleteRequiresAdmin: true,
            TotpCodeLength: 8,
            TotpRecoveryCodesCount: 15,
            TotpRecoveryCodeLength: 12,
            JwtRefreshTokenDays: 14,
            JwtIssuer: "CustomIssuer",
            JwtAudience: "CustomAudience",
            MaxWorkspaceNameLength: 120,
            MaxBoardNameLength: 150,
            MaxBoardDescriptionLength: 3000,
            MaxListNameLength: 110,
            MaxDisplayNameLength: 90,
            MaxCardTitleLength: 600,
            MaxCardDescriptionLength: 20000,
            MaxChecklistItemLength: 600,
            MaxAttachmentsPerCard: 75,
            AllowedAvatarExtensions: "png,jpg,webp",
            MaxAvatarSizeMb: 5,
            EmailRateLimitPerMinute: 120,
            EmailBatchSize: 40,
            EmailIncludeUnsubscribeLink: true,
            DefaultPageSize: 25,
            MaxPageSize: 100,
            ActivityFeedPageSize: 30,
            ResponseCompressionEnabled: false,
            ResponseCompressionLevel: "Fastest",
            StaticFilesMaxAgeSeconds: 3600,
            ForwardedHeadersEnabled: false,
            MaxRequestBodySizeMb: 50,
            DatabaseCommandTimeoutSeconds: 45,
            DatabaseMaxRetryCount: 5,
            DatabaseMaxRetryDelaySeconds: 10,
            DatabaseEnableDetailedErrors: true,
            DatabaseEnableSensitiveDataLogging: true,
            RunMigrationsOnStartup: false,
            EnableSwaggerInProduction: true);

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
        updated.CustomLogoUrl.Should().Be("/images/logo-custom.png");
        updated.HidePoweredByCardscape.Should().BeTrue();
        updated.IdleSessionTimeoutMinutes.Should().Be(30);
        updated.PasswordExpiryDays.Should().Be(90);
        updated.DefaultBoardView.Should().Be("Timeline");
        updated.EnableTimeTracking.Should().BeTrue();
        updated.AutomationMonthlyRunQuotaPerUser.Should().Be(5000);
        updated.EnableWebPushNotifications.Should().BeTrue();
        updated.VapidPrivateKeyMasked.Should().Be("******");
        updated.MicrosoftTeamsIntegrationEnabled.Should().BeTrue();
        updated.MicrosoftTeamsWebhookUrlMasked.Should().Be("******");
        updated.DiscordIntegrationEnabled.Should().BeTrue();
        updated.DiscordWebhookUrlMasked.Should().Be("******");
        updated.GitLabIntegrationEnabled.Should().BeTrue();
        updated.GitLabEndpoint.Should().Be("https://gitlab.myorg.internal");
        updated.DisplayCookieBanner.Should().BeTrue();
        updated.AutoBackupEnabled.Should().BeTrue();
        updated.BackupIntervalHours.Should().Be(12);
        updated.MaintenanceModeEnabled.Should().BeTrue();
        updated.MaintenanceModeMessage.Should().Be("Mantenimiento urgente");
        updated.SystemAnnouncementEnabled.Should().BeTrue();
        updated.SystemAnnouncementMessage.Should().Be("Aviso a todos los usuarios");
        updated.SystemAnnouncementType.Should().Be("Warning");
        updated.HstsMaxAgeSeconds.Should().Be(63072000);
        updated.HstsIncludeSubdomains.Should().BeTrue();
        updated.HstsPreload.Should().BeTrue();
        updated.ContentSecurityPolicy.Should().Be("default-src 'self';");
        updated.XFrameOptions.Should().Be("SAMEORIGIN");
        updated.ReferrerPolicy.Should().Be("strict-origin-when-cross-origin");
        updated.DataProtectionKeyLifetimeDays.Should().Be(180);
        updated.DataProtectionKeyDirectory.Should().Be("/keys/dir");
        updated.EnableAppleAuth.Should().BeTrue();
        updated.AppleClientId.Should().Be("com.apple.test");
        updated.AppleTeamId.Should().Be("TEAMXYZ");
        updated.AppleKeyId.Should().Be("KEYXYZ");
        updated.ApplePrivateKeyPemMasked.Should().Be("******");
        updated.S3BucketName.Should().Be("my-bucket");
        updated.S3EndpointUrl.Should().Be("http://minio:9000");
        updated.S3Region.Should().Be("us-west-2");
        updated.S3AccessKey.Should().Be("S3ACCESS");
        updated.S3SecretKeyMasked.Should().Be("******");
        updated.S3ForcePathStyle.Should().BeTrue();
        updated.BlockExecutableAttachments.Should().BeTrue();
        updated.ScanAttachmentsForMalware.Should().BeTrue();
        updated.ClamAvDaemonEndpoint.Should().Be("tcp://clamav:3310");
        updated.SlackIntegrationEnabled.Should().BeTrue();
        updated.SlackClientId.Should().Be("slack-id-123");
        updated.SlackClientSecretMasked.Should().Be("******");
        updated.SlackSigningSecretMasked.Should().Be("******");
        updated.SlackBotTokenMasked.Should().Be("******");
        updated.GitHubIntegrationEnabled.Should().BeTrue();
        updated.GitHubTokenMasked.Should().Be("******");
        updated.GitHubSyncIntervalMinutes.Should().Be(30);
        updated.GitHubAutoCloseCardsOnPrMerge.Should().BeTrue();
        updated.GoogleCalendarIntegrationEnabled.Should().BeTrue();
        updated.GoogleCalendarClientId.Should().Be("gcal-client-id-val");
        updated.GoogleCalendarClientSecretMasked.Should().Be("******");
        updated.GoogleCalendarSyncIntervalMinutes.Should().Be(20);
        updated.OtelTracingEnabled.Should().BeTrue();
        updated.OtelMetricsEnabled.Should().BeTrue();
        updated.OtelEndpointUrl.Should().Be("http://otel:4317");
        updated.OtelServiceName.Should().Be("Cardscape.Custom");
        updated.OtelTraceSampleRate.Should().Be(50);
        updated.OutboxProcessorEnabled.Should().BeTrue();
        updated.OutboxBatchSize.Should().Be(100);
        updated.OutboxPollIntervalSeconds.Should().Be(2);
        updated.OutboxMaxAttempts.Should().Be(10);
        updated.OutboxMessageRetentionDays.Should().Be(30);
        updated.SoftDeleteRetentionDays.Should().Be(60);
        updated.PermanentDeleteRequiresAdmin.Should().BeTrue();
        updated.TotpCodeLength.Should().Be(8);
        updated.TotpRecoveryCodesCount.Should().Be(15);
        updated.TotpRecoveryCodeLength.Should().Be(12);
        updated.JwtRefreshTokenDays.Should().Be(14);
        updated.JwtIssuer.Should().Be("CustomIssuer");
        updated.JwtAudience.Should().Be("CustomAudience");
        updated.MaxWorkspaceNameLength.Should().Be(120);
        updated.MaxBoardNameLength.Should().Be(150);
        updated.MaxBoardDescriptionLength.Should().Be(3000);
        updated.MaxListNameLength.Should().Be(110);
        updated.MaxDisplayNameLength.Should().Be(90);
        updated.MaxCardTitleLength.Should().Be(600);
        updated.MaxCardDescriptionLength.Should().Be(20000);
        updated.MaxChecklistItemLength.Should().Be(600);
        updated.MaxAttachmentsPerCard.Should().Be(75);
        updated.AllowedAvatarExtensions.Should().Be("png,jpg,webp");
        updated.MaxAvatarSizeMb.Should().Be(5);
        updated.EmailRateLimitPerMinute.Should().Be(120);
        updated.EmailBatchSize.Should().Be(40);
        updated.EmailIncludeUnsubscribeLink.Should().BeTrue();
        updated.DefaultPageSize.Should().Be(25);
        updated.MaxPageSize.Should().Be(100);
        updated.ActivityFeedPageSize.Should().Be(30);
        updated.ResponseCompressionEnabled.Should().BeFalse();
        updated.ResponseCompressionLevel.Should().Be("Fastest");
        updated.StaticFilesMaxAgeSeconds.Should().Be(3600);
        updated.ForwardedHeadersEnabled.Should().BeFalse();
        updated.MaxRequestBodySizeMb.Should().Be(50);
        updated.DatabaseCommandTimeoutSeconds.Should().Be(45);
        updated.DatabaseMaxRetryCount.Should().Be(5);
        updated.DatabaseMaxRetryDelaySeconds.Should().Be(10);
        updated.DatabaseEnableDetailedErrors.Should().BeTrue();
        updated.DatabaseEnableSensitiveDataLogging.Should().BeTrue();
        updated.RunMigrationsOnStartup.Should().BeFalse();
        updated.EnableSwaggerInProduction.Should().BeTrue();

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
