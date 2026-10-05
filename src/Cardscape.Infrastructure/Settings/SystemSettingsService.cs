using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Settings;

public sealed class SystemSettingsService : ISystemSettingsService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemSettingsService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _dataDir;
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private PersistedSettingsModel? _cached;

    public SystemSettingsService(
        IConfiguration configuration,
        ILogger<SystemSettingsService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;

        _dataDir = configuration["Cardscape:DataRoot"]
            ?? (Directory.Exists("/app/Data")
                ? "/app/Data"
                : Path.Combine(Directory.GetCurrentDirectory(), "Data"));

        try
        {
            Directory.CreateDirectory(_dataDir);
        }
        catch
        {
            // Best effort; directory may already exist.
        }

        _filePath = Path.Combine(_dataDir, "system_settings.json");
    }

    public async Task<SystemSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        PersistedSettingsModel model = await GetPersistedModelAsync(ct);
        return MapToDto(model);
    }

    public async Task<SystemSettingsDto> UpdateSettingsAsync(
        UpdateSystemSettingsRequest request,
        string? updatedBy,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            PersistedSettingsModel current = await LoadFromFileOrDefaultsInternalAsync(ct);

            // Preserve existing secrets if mask is passed
            string resolvedAiApiKey = (request.AiApiKey is null || request.AiApiKey == "******")
                ? current.AiApiKey
                : request.AiApiKey.Trim();

            string resolvedSmtpPassword = (request.SmtpPassword is null || request.SmtpPassword == "******")
                ? current.SmtpPassword
                : request.SmtpPassword;

            string resolvedRedisConn = (request.RedisConnectionString is null || request.RedisConnectionString == "******")
                ? current.RedisConnectionString
                : request.RedisConnectionString.Trim();

            string resolvedGoogleSecret = (request.GoogleClientSecret is null || request.GoogleClientSecret == "******")
                ? current.GoogleClientSecret
                : request.GoogleClientSecret.Trim();

            string resolvedGitHubSecret = (request.GitHubClientSecret is null || request.GitHubClientSecret == "******")
                ? current.GitHubClientSecret
                : request.GitHubClientSecret.Trim();

            string resolvedMicrosoftSecret = (request.MicrosoftClientSecret is null || request.MicrosoftClientSecret == "******")
                ? current.MicrosoftClientSecret
                : request.MicrosoftClientSecret.Trim();

            string resolvedApplePrivateKey = (request.ApplePrivateKeyPem is null || request.ApplePrivateKeyPem == "******")
                ? current.ApplePrivateKeyPem
                : request.ApplePrivateKeyPem.Trim();

            string resolvedVapidSecret = (request.VapidPrivateKey is null || request.VapidPrivateKey == "******")
                ? current.VapidPrivateKey
                : request.VapidPrivateKey.Trim();

            string resolvedTeamsUrl = (request.MicrosoftTeamsWebhookUrl is null || request.MicrosoftTeamsWebhookUrl == "******")
                ? current.MicrosoftTeamsWebhookUrl
                : request.MicrosoftTeamsWebhookUrl.Trim();

            string resolvedDiscordUrl = (request.DiscordWebhookUrl is null || request.DiscordWebhookUrl == "******")
                ? current.DiscordWebhookUrl
                : request.DiscordWebhookUrl.Trim();

            string resolvedS3SecretKey = (request.S3SecretKey is null || request.S3SecretKey == "******")
                ? current.S3SecretKey
                : request.S3SecretKey.Trim();

            string resolvedGitHubToken = (request.GitHubToken is null || request.GitHubToken == "******")
                ? current.GitHubToken
                : request.GitHubToken.Trim();

            string resolvedGoogleCalendarSecret = (request.GoogleCalendarClientSecret is null || request.GoogleCalendarClientSecret == "******")
                ? current.GoogleCalendarClientSecret
                : request.GoogleCalendarClientSecret.Trim();

            string resolvedSlackClientSecret = (request.SlackClientSecret is null || request.SlackClientSecret == "******")
                ? current.SlackClientSecret
                : request.SlackClientSecret.Trim();

            string resolvedSlackSigningSecret = (request.SlackSigningSecret is null || request.SlackSigningSecret == "******")
                ? current.SlackSigningSecret
                : request.SlackSigningSecret.Trim();

            string resolvedSlackBotToken = (request.SlackBotToken is null || request.SlackBotToken == "******")
                ? current.SlackBotToken
                : request.SlackBotToken.Trim();

            var updated = new PersistedSettingsModel
            {
                // 1. General, Brand & Whitelabel
                InstanceTitle = string.IsNullOrWhiteSpace(request.InstanceTitle) ? "Cardscape" : request.InstanceTitle.Trim(),
                SupportEmail = string.IsNullOrWhiteSpace(request.SupportEmail) ? "support@cardscape.local" : request.SupportEmail.Trim(),
                DefaultLanguage = string.Equals(request.DefaultLanguage, "es", StringComparison.OrdinalIgnoreCase) ? "es" : "en",
                DefaultTheme = string.IsNullOrWhiteSpace(request.DefaultTheme) ? "default" : request.DefaultTheme.Trim(),
                AllowPublicRegistration = request.AllowPublicRegistration,
                WelcomeMessage = string.IsNullOrWhiteSpace(request.WelcomeMessage) ? "Bienvenido a Cardscape" : request.WelcomeMessage.Trim(),
                EnableKeyboardShortcuts = request.EnableKeyboardShortcuts,
                EnableDenseModeByDefault = request.EnableDenseModeByDefault,
                ShowCardCoverImagesByDefault = request.ShowCardCoverImagesByDefault,
                EnableSoundNotifications = request.EnableSoundNotifications,
                CustomLogoUrl = request.CustomLogoUrl?.Trim() ?? string.Empty,
                CustomFaviconUrl = request.CustomFaviconUrl?.Trim() ?? string.Empty,
                HidePoweredByCardscape = request.HidePoweredByCardscape,
                MaintenanceModeEnabled = request.MaintenanceModeEnabled,
                MaintenanceModeMessage = string.IsNullOrWhiteSpace(request.MaintenanceModeMessage) ? "El sistema se encuentra en mantenimiento programado." : request.MaintenanceModeMessage.Trim(),
                SystemAnnouncementEnabled = request.SystemAnnouncementEnabled,
                SystemAnnouncementMessage = request.SystemAnnouncementMessage?.Trim() ?? string.Empty,
                SystemAnnouncementType = string.IsNullOrWhiteSpace(request.SystemAnnouncementType) ? "Info" : request.SystemAnnouncementType.Trim(),

                // 2. Security & Policy
                JwtAccessTokenMinutes = Math.Clamp(request.JwtAccessTokenMinutes, 5, 43200),
                PasswordMinLength = Math.Clamp(request.PasswordMinLength, 6, 32),
                PasswordRequireDigit = request.PasswordRequireDigit,
                PasswordRequireNonAlphanumeric = request.PasswordRequireNonAlphanumeric,
                RequireTwoFactorForAdmins = request.RequireTwoFactorForAdmins,
                MaxFailedLoginAttempts = Math.Clamp(request.MaxFailedLoginAttempts, 3, 20),
                LockoutDurationMinutes = Math.Clamp(request.LockoutDurationMinutes, 1, 1440),
                SingleActiveSessionPerUser = request.SingleActiveSessionPerUser,
                MaxApiTokensPerUser = Math.Clamp(request.MaxApiTokensPerUser, 1, 100),
                ApiTokenExpirationDays = Math.Clamp(request.ApiTokenExpirationDays, 1, 365),
                DefaultApiTokenExpiryDays = Math.Clamp(request.DefaultApiTokenExpiryDays, 1, 365),
                MaxApiTokenExpiryDays = Math.Clamp(request.MaxApiTokenExpiryDays, 1, 1825),
                CacheAdminClaim = request.CacheAdminClaim,
                TotpIssuerName = string.IsNullOrWhiteSpace(request.TotpIssuerName) ? "Cardscape" : request.TotpIssuerName.Trim(),
                TotpStepTolerance = Math.Clamp(request.TotpStepTolerance, 0, 3),
                PasswordResetTokenLifetimeMinutes = Math.Clamp(request.PasswordResetTokenLifetimeMinutes, 5, 1440),
                IdleSessionTimeoutMinutes = Math.Clamp(request.IdleSessionTimeoutMinutes, 0, 1440),
                MaxConcurrentSessionsPerUser = Math.Clamp(request.MaxConcurrentSessionsPerUser, 1, 50),
                PasswordExpiryDays = Math.Clamp(request.PasswordExpiryDays, 0, 365),
                PasswordHistoryCount = Math.Clamp(request.PasswordHistoryCount, 0, 24),
                CorsAllowedOrigins = string.IsNullOrWhiteSpace(request.CorsAllowedOrigins) ? "*" : request.CorsAllowedOrigins.Trim(),
                EnforceHttps = request.EnforceHttps,
                EnableSecurityHeaders = request.EnableSecurityHeaders,
                HstsMaxAgeSeconds = Math.Clamp(request.HstsMaxAgeSeconds, 0, 63072000),
                HstsIncludeSubdomains = request.HstsIncludeSubdomains,
                HstsPreload = request.HstsPreload,
                ContentSecurityPolicy = request.ContentSecurityPolicy?.Trim() ?? string.Empty,
                XFrameOptions = string.IsNullOrWhiteSpace(request.XFrameOptions) ? "DENY" : request.XFrameOptions.Trim(),
                ReferrerPolicy = string.IsNullOrWhiteSpace(request.ReferrerPolicy) ? "no-referrer" : request.ReferrerPolicy.Trim(),
                DataProtectionKeyLifetimeDays = Math.Clamp(request.DataProtectionKeyLifetimeDays, 14, 365),
                DataProtectionKeyDirectory = request.DataProtectionKeyDirectory?.Trim() ?? string.Empty,

                // 3. Enterprise SSO & Provisioning
                SamlSsoEnabled = request.SamlSsoEnabled,
                SamlEnforceForDomain = request.SamlEnforceForDomain?.Trim() ?? string.Empty,
                ScimProvisioningEnabled = request.ScimProvisioningEnabled,
                ScimTokenExpirationDays = Math.Clamp(request.ScimTokenExpirationDays, 1, 365),
                OAuthAppsEnabled = request.OAuthAppsEnabled,
                MaxOAuthAppsPerUser = Math.Clamp(request.MaxOAuthAppsPerUser, 1, 50),

                // 4. External / Social OAuth
                EnableGoogleAuth = request.EnableGoogleAuth,
                GoogleClientId = request.GoogleClientId?.Trim() ?? string.Empty,
                GoogleClientSecret = resolvedGoogleSecret,
                EnableGitHubAuth = request.EnableGitHubAuth,
                GitHubClientId = request.GitHubClientId?.Trim() ?? string.Empty,
                GitHubClientSecret = resolvedGitHubSecret,
                EnableMicrosoftAuth = request.EnableMicrosoftAuth,
                MicrosoftClientId = request.MicrosoftClientId?.Trim() ?? string.Empty,
                MicrosoftClientSecret = resolvedMicrosoftSecret,
                EnableAppleAuth = request.EnableAppleAuth,
                AppleClientId = request.AppleClientId?.Trim() ?? string.Empty,
                AppleTeamId = request.AppleTeamId?.Trim() ?? string.Empty,
                AppleKeyId = request.AppleKeyId?.Trim() ?? string.Empty,
                ApplePrivateKeyPem = resolvedApplePrivateKey,

                // 5. Workspaces & Boards
                MaxWorkspacesPerUser = Math.Clamp(request.MaxWorkspacesPerUser, 0, 1000),
                MaxBoardsPerWorkspace = Math.Clamp(request.MaxBoardsPerWorkspace, 0, 1000),
                MaxMembersPerWorkspace = Math.Clamp(request.MaxMembersPerWorkspace, 0, 1000),
                DefaultWorkspaceRole = string.Equals(request.DefaultWorkspaceRole, "Viewer", StringComparison.OrdinalIgnoreCase) ? "Viewer" : "Member",
                InvitationExpirationDays = Math.Clamp(request.InvitationExpirationDays, 1, 90),
                AllowPublicBoards = request.AllowPublicBoards,
                EnablePublicBoardTemplates = request.EnablePublicBoardTemplates,
                AllowCustomUserTemplates = request.AllowCustomUserTemplates,

                // 6. Cards, Lists & Productivity
                DefaultWipLimit = Math.Clamp(request.DefaultWipLimit, 0, 100),
                EnforceWipLimits = request.EnforceWipLimits,
                HighlightOverLimitLists = request.HighlightOverLimitLists,
                AllowCardMirroring = request.AllowCardMirroring,
                AllowCardSnoozing = request.AllowCardSnoozing,
                AllowCardVoting = request.AllowCardVoting,
                MaxVotesPerUserPerCard = Math.Clamp(request.MaxVotesPerUserPerCard, 1, 100),
                MaxChecklistsPerCard = Math.Clamp(request.MaxChecklistsPerCard, 1, 50),
                AutoArchiveCompletedCardsDays = Math.Clamp(request.AutoArchiveCompletedCardsDays, 0, 365),
                CardRecurrenceEnabled = request.CardRecurrenceEnabled,
                MaxRecurrenceIntervalDays = Math.Clamp(request.MaxRecurrenceIntervalDays, 1, 730),
                EnableTimelineView = request.EnableTimelineView,
                EnableCalendarView = request.EnableCalendarView,
                EnableTableView = request.EnableTableView,
                DefaultBoardView = string.IsNullOrWhiteSpace(request.DefaultBoardView) ? "Kanban" : request.DefaultBoardView.Trim(),

                // 7. Time Tracking & Estimates
                EnableTimeTracking = request.EnableTimeTracking,
                EnforceTimeTrackingEstimates = request.EnforceTimeTrackingEstimates,
                TimeTrackingUnit = string.IsNullOrWhiteSpace(request.TimeTrackingUnit) ? "Hours" : request.TimeTrackingUnit.Trim(),

                // 8. Comments & Collaboration
                AllowCommentEditing = request.AllowCommentEditing,
                AllowCommentDeletion = request.AllowCommentDeletion,
                MaxCommentLength = Math.Clamp(request.MaxCommentLength, 100, 50000),
                AllowUserMentions = request.AllowUserMentions,

                // 9. Labels & Custom Fields
                MaxLabelsPerBoard = Math.Clamp(request.MaxLabelsPerBoard, 5, 200),
                MaxLabelsPerCard = Math.Clamp(request.MaxLabelsPerCard, 1, 50),
                CustomFieldsEnabled = request.CustomFieldsEnabled,
                MaxCustomFieldsPerBoard = Math.Clamp(request.MaxCustomFieldsPerBoard, 1, 100),

                // 10. Board Automation Rules
                BoardAutomationEnabled = request.BoardAutomationEnabled,
                MaxAutomationRulesPerBoard = Math.Clamp(request.MaxAutomationRulesPerBoard, 1, 100),
                MaxAutomationActionsPerRule = Math.Clamp(request.MaxAutomationActionsPerRule, 1, 20),
                AutomationMonthlyRunQuotaPerUser = Math.Clamp(request.AutomationMonthlyRunQuotaPerUser, 10, 1000000),
                AutomationTimeoutSeconds = Math.Clamp(request.AutomationTimeoutSeconds, 1, 120),

                // 11. Notifications & System Alerts
                InAppNotificationsEnabled = request.InAppNotificationsEnabled,
                DueSoonThresholdHours = Math.Clamp(request.DueSoonThresholdHours, 1, 168),
                NotifyOnCardAssignment = request.NotifyOnCardAssignment,
                NotifyOnCardMention = request.NotifyOnCardMention,
                NotifyOnDueSoon = request.NotifyOnDueSoon,
                NotifyOnOverdue = request.NotifyOnOverdue,
                NotificationRetentionDays = Math.Clamp(request.NotificationRetentionDays, 1, 365),
                EnableWebPushNotifications = request.EnableWebPushNotifications,
                VapidSubject = string.IsNullOrWhiteSpace(request.VapidSubject) ? "mailto:admin@cardscape.local" : request.VapidSubject.Trim(),
                VapidPublicKey = request.VapidPublicKey?.Trim() ?? string.Empty,
                VapidPrivateKey = resolvedVapidSecret,

                // 12. Dashboards & Metric Cards
                EnableDashboards = request.EnableDashboards,
                MaxDashcardsPerBoard = Math.Clamp(request.MaxDashcardsPerBoard, 1, 50),
                DashboardRefreshIntervalSeconds = Math.Clamp(request.DashboardRefreshIntervalSeconds, 10, 3600),

                // 13. Import & Export
                EnableBoardExport = request.EnableBoardExport,
                EnableKanbanImport = request.EnableKanbanImport,
                MaxImportFileSizeMb = Math.Clamp(request.MaxImportFileSizeMb, 1, 500),

                // 14. Search & Indexing
                SearchMinQueryLength = Math.Clamp(request.SearchMinQueryLength, 1, 10),
                SearchMaxPageSize = Math.Clamp(request.SearchMaxPageSize, 5, 200),
                SearchFuzzyMatching = request.SearchFuzzyMatching,

                // 15. Realtime & Presence
                RealtimeBroadcastingEnabled = request.RealtimeBroadcastingEnabled,
                RealtimePresenceEnabled = request.RealtimePresenceEnabled,

                // 16. Card Aging
                CardAgingEnabled = request.CardAgingEnabled,
                CardAgingInactiveDays = Math.Clamp(request.CardAgingInactiveDays, 1, 365),
                CardAgingMode = string.Equals(request.CardAgingMode, "Pirate", StringComparison.OrdinalIgnoreCase) ? "Pirate" : "Regular",

                // 17. Storage & Attachments
                MaxAttachmentSizeMb = Math.Clamp(request.MaxAttachmentSizeMb, 1, 200),
                AllowedAttachmentExtensions = string.IsNullOrWhiteSpace(request.AllowedAttachmentExtensions)
                    ? "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip"
                    : request.AllowedAttachmentExtensions.Trim(),
                AllowCoverImages = request.AllowCoverImages,
                MaxCoverImageSizeMb = Math.Clamp(request.MaxCoverImageSizeMb, 1, 25),
                S3BucketName = request.S3BucketName?.Trim() ?? string.Empty,
                S3EndpointUrl = request.S3EndpointUrl?.Trim() ?? string.Empty,
                S3Region = string.IsNullOrWhiteSpace(request.S3Region) ? "us-east-1" : request.S3Region.Trim(),
                S3AccessKey = request.S3AccessKey?.Trim() ?? string.Empty,
                S3SecretKey = resolvedS3SecretKey,
                S3ForcePathStyle = request.S3ForcePathStyle,
                BlockExecutableAttachments = request.BlockExecutableAttachments,
                ScanAttachmentsForMalware = request.ScanAttachmentsForMalware,
                ClamAvDaemonEndpoint = request.ClamAvDaemonEndpoint?.Trim() ?? string.Empty,

                // 18. Artificial Intelligence
                AiEnabled = request.AiEnabled,
                AiProvider = string.IsNullOrWhiteSpace(request.AiProvider) ? "OpenAiCompatible" : request.AiProvider.Trim(),
                AiEndpoint = string.IsNullOrWhiteSpace(request.AiEndpoint) ? "http://localhost:11434/" : request.AiEndpoint.Trim(),
                AiModel = string.IsNullOrWhiteSpace(request.AiModel) ? "llama3.2" : request.AiModel.Trim(),
                AiApiKey = resolvedAiApiKey,
                AiTimeoutSeconds = Math.Clamp(request.AiTimeoutSeconds, 5, 300),
                AiMaxTokens = Math.Clamp(request.AiMaxTokens, 128, 32768),
                AiEnableCardDescriptionGen = request.AiEnableCardDescriptionGen,
                AiEnableCommentSummary = request.AiEnableCommentSummary,
                AiEnableAutoChecklists = request.AiEnableAutoChecklists,
                AiTemperature = Math.Clamp(request.AiTemperature, 0, 100),

                // 19. Outbound Email (SMTP)
                EmailNotificationsEnabled = request.EmailNotificationsEnabled,
                SmtpHost = string.IsNullOrWhiteSpace(request.SmtpHost) ? "localhost" : request.SmtpHost.Trim(),
                SmtpPort = Math.Clamp(request.SmtpPort, 1, 65535),
                SmtpUsername = request.SmtpUsername?.Trim() ?? string.Empty,
                SmtpPassword = resolvedSmtpPassword,
                SmtpEnableSsl = request.SmtpEnableSsl,
                SenderEmail = string.IsNullOrWhiteSpace(request.SenderEmail) ? "noreply@cardscape.local" : request.SenderEmail.Trim(),
                SenderName = string.IsNullOrWhiteSpace(request.SenderName) ? "Cardscape Notificaciones" : request.SenderName.Trim(),

                // 20. Inbound Email (Email-to-Board)
                InboundEmailEnabled = request.InboundEmailEnabled,
                InboundEmailDomain = string.IsNullOrWhiteSpace(request.InboundEmailDomain) ? "inbound.cardscape.local" : request.InboundEmailDomain.Trim(),
                InboundDefaultList = string.IsNullOrWhiteSpace(request.InboundDefaultList) ? "Inbox" : request.InboundDefaultList.Trim(),
                InboundAttachSenderEmail = request.InboundAttachSenderEmail,

                // 21. Integrations (Slack, GitHub, Calendar, MCP, Cloud)
                SlackIntegrationEnabled = request.SlackIntegrationEnabled,
                SlackClientId = request.SlackClientId?.Trim() ?? string.Empty,
                SlackClientSecret = resolvedSlackClientSecret,
                SlackSigningSecret = resolvedSlackSigningSecret,
                SlackBotToken = resolvedSlackBotToken,
                GitHubIntegrationEnabled = request.GitHubIntegrationEnabled,
                GitHubToken = resolvedGitHubToken,
                GitHubSyncIntervalMinutes = Math.Clamp(request.GitHubSyncIntervalMinutes, 1, 1440),
                GitHubAutoCloseCardsOnPrMerge = request.GitHubAutoCloseCardsOnPrMerge,
                GoogleCalendarIntegrationEnabled = request.GoogleCalendarIntegrationEnabled,
                GoogleCalendarClientId = request.GoogleCalendarClientId?.Trim() ?? string.Empty,
                GoogleCalendarClientSecret = resolvedGoogleCalendarSecret,
                GoogleCalendarSyncIntervalMinutes = Math.Clamp(request.GoogleCalendarSyncIntervalMinutes, 1, 1440),
                McpServerEnabled = request.McpServerEnabled,
                CalendarIcsFeedsEnabled = request.CalendarIcsFeedsEnabled,
                CalendarFeedTokenLifetimeDays = Math.Clamp(request.CalendarFeedTokenLifetimeDays, 7, 730),
                GoogleDriveIntegrationEnabled = request.GoogleDriveIntegrationEnabled,
                OneDriveIntegrationEnabled = request.OneDriveIntegrationEnabled,
                DropboxIntegrationEnabled = request.DropboxIntegrationEnabled,
                MicrosoftTeamsIntegrationEnabled = request.MicrosoftTeamsIntegrationEnabled,
                MicrosoftTeamsWebhookUrl = resolvedTeamsUrl,
                DiscordIntegrationEnabled = request.DiscordIntegrationEnabled,
                DiscordWebhookUrl = resolvedDiscordUrl,
                GitLabIntegrationEnabled = request.GitLabIntegrationEnabled,
                GitLabEndpoint = request.GitLabEndpoint?.Trim() ?? string.Empty,

                // 22. Model Context Protocol (MCP Server)
                McpServerName = string.IsNullOrWhiteSpace(request.McpServerName) ? "Cardscape-MCP" : request.McpServerName.Trim(),
                McpEnableWriteTools = request.McpEnableWriteTools,
                McpMaxBatchSize = Math.Clamp(request.McpMaxBatchSize, 5, 200),

                // 23. Webhooks
                WebhooksEnabled = request.WebhooksEnabled,
                MaxWebhookRetries = Math.Clamp(request.MaxWebhookRetries, 0, 10),
                WebhookTimeoutSeconds = Math.Clamp(request.WebhookTimeoutSeconds, 1, 60),
                WebhookPayloadSignatureEnabled = request.WebhookPayloadSignatureEnabled,

                // 24. API Idempotency
                EnableIdempotency = request.EnableIdempotency,
                IdempotencyReservationWindowMinutes = Math.Clamp(request.IdempotencyReservationWindowMinutes, 1, 60),
                IdempotencyRetentionWindowHours = Math.Clamp(request.IdempotencyRetentionWindowHours, 1, 168),

                // 25. Rate Limiting & Performance
                RateLimitingEnabled = request.RateLimitingEnabled,
                DefaultRequestsPerHour = Math.Clamp(request.DefaultRequestsPerHour, 10, 1000000),
                RateLimiterBackend = string.Equals(request.RateLimiterBackend, "Redis", StringComparison.OrdinalIgnoreCase) ? "Redis" : "InMemory",
                BackgroundJobPollIntervalSeconds = Math.Clamp(request.BackgroundJobPollIntervalSeconds, 1, 60),
                BackgroundJobBatchSize = Math.Clamp(request.BackgroundJobBatchSize, 1, 100),

                // 26. Infrastructure, Redis & Observability
                RedisConnectionString = resolvedRedisConn,
                RedisDatabase = Math.Clamp(request.RedisDatabase, 0, 15),
                PendingTotpStoreBackend = string.Equals(request.PendingTotpStoreBackend, "Redis", StringComparison.OrdinalIgnoreCase) ? "Redis" : "InMemory",
                PendingTotpStoreKeyPrefix = string.IsNullOrWhiteSpace(request.PendingTotpStoreKeyPrefix) ? "cardscape:totp-pending:" : request.PendingTotpStoreKeyPrefix.Trim(),
                RateLimiterKeyPrefix = string.IsNullOrWhiteSpace(request.RateLimiterKeyPrefix) ? "cardscape:rl:" : request.RateLimiterKeyPrefix.Trim(),
                OtelTracingEnabled = request.OtelTracingEnabled,
                OtelMetricsEnabled = request.OtelMetricsEnabled,
                OtelEndpointUrl = request.OtelEndpointUrl?.Trim() ?? string.Empty,
                OtelServiceName = string.IsNullOrWhiteSpace(request.OtelServiceName) ? "Cardscape.Api" : request.OtelServiceName.Trim(),
                OtelTraceSampleRate = Math.Clamp(request.OtelTraceSampleRate, 0, 100),
                OutboxProcessorEnabled = request.OutboxProcessorEnabled,
                OutboxBatchSize = Math.Clamp(request.OutboxBatchSize, 1, 500),
                OutboxPollIntervalSeconds = Math.Clamp(request.OutboxPollIntervalSeconds, 1, 60),
                OutboxMaxAttempts = Math.Clamp(request.OutboxMaxAttempts, 1, 10),
                OutboxMessageRetentionDays = Math.Clamp(request.OutboxMessageRetentionDays, 1, 90),

                // 27. Activities & Audit Logging
                EnableActivityLogging = request.EnableActivityLogging,
                ActivityPageSize = Math.Clamp(request.ActivityPageSize, 5, 100),
                AuditLogDetailedIp = request.AuditLogDetailedIp,

                // 28. System Logging (Serilog)
                LogRetainedFileCountLimit = Math.Clamp(request.LogRetainedFileCountLimit, 1, 365),
                LogFileSizeLimitMb = Math.Clamp(request.LogFileSizeLimitMb, 5, 1024),

                // 29. Data Retention, Residency & GDPR
                RetentionSweeperEnabled = request.RetentionSweeperEnabled,
                SweepIntervalHours = Math.Clamp(request.SweepIntervalHours, 1, 168),
                UserGracePeriodDays = Math.Clamp(request.UserGracePeriodDays, 0, 365),
                ActivityRetentionDays = Math.Clamp(request.ActivityRetentionDays, 1, 3650),
                AuditRetentionDays = Math.Clamp(request.AuditRetentionDays, 1, 3650),
                AutoPurgeOrphanAttachments = request.AutoPurgeOrphanAttachments,
                DataResidencyEnabled = request.DataResidencyEnabled,
                DeploymentRegion = string.IsNullOrWhiteSpace(request.DeploymentRegion) ? "Unspecified" : request.DeploymentRegion.Trim(),
                EnforceDataResidency = request.EnforceDataResidency,
                SoftDeleteRetentionDays = Math.Clamp(request.SoftDeleteRetentionDays, 1, 365),
                PermanentDeleteRequiresAdmin = request.PermanentDeleteRequiresAdmin,

                // 30. Legal Notices, Privacy & Compliance
                CustomPrivacyPolicyUrl = request.CustomPrivacyPolicyUrl?.Trim() ?? string.Empty,
                CustomTermsOfServiceUrl = request.CustomTermsOfServiceUrl?.Trim() ?? string.Empty,
                DisplayCookieBanner = request.DisplayCookieBanner,
                RequireLegalNoticeAcceptance = request.RequireLegalNoticeAcceptance,

                // 31. Automated Backups
                AutoBackupEnabled = request.AutoBackupEnabled,
                BackupIntervalHours = Math.Clamp(request.BackupIntervalHours, 1, 168),
                BackupRetentionDays = Math.Clamp(request.BackupRetentionDays, 1, 365),

                // 32. Seeder & Dev
                SeederEnabled = request.SeederEnabled,
                AllowSeederExecution = request.AllowSeederExecution,
                SeederWipeBeforeSeed = request.SeederWipeBeforeSeed
            };

            await SaveToFileAsync(updated, ct);
            _cached = updated;

            InfrastructureSettingsLogMessages.SettingsUpdated(_logger, updatedBy ?? "system", updated.InstanceTitle);
            return MapToDto(updated);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<SystemSettingsDto> ResetToDefaultsAsync(string? resetBy, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var defaults = new PersistedSettingsModel();
            await SaveToFileAsync(defaults, ct);
            _cached = defaults;

            InfrastructureSettingsLogMessages.SettingsReset(_logger, resetBy ?? "system");
            return MapToDto(defaults);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> IsPublicRegistrationAllowedAsync(CancellationToken ct = default)
    {
        PersistedSettingsModel model = await GetPersistedModelAsync(ct);
        return model.AllowPublicRegistration;
    }

    public async Task<TestAiResponse> TestAiConnectionAsync(CancellationToken ct = default)
    {
        PersistedSettingsModel model = await GetPersistedModelAsync(ct);
        if (!model.AiEnabled)
        {
            return new TestAiResponse(false, "El asistente de IA no está habilitado en la configuración.");
        }

        if (string.IsNullOrWhiteSpace(model.AiEndpoint))
        {
            return new TestAiResponse(false, "El endpoint del servicio de IA no está configurado.");
        }

        try
        {
            HttpClient client = _httpClientFactory.CreateClient("AiServiceTest");
            client.Timeout = TimeSpan.FromSeconds(Math.Min(model.AiTimeoutSeconds, 15));

            var request = new HttpRequestMessage(HttpMethod.Get, model.AiEndpoint);
            if (!string.IsNullOrWhiteSpace(model.AiApiKey))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", model.AiApiKey);
            }

            HttpResponseMessage response = await client.SendAsync(request, ct);
            return new TestAiResponse(
                true,
                $"Conexión exitosa con el servicio IA (HTTP {(int)response.StatusCode}). Modelo activo: {model.AiModel}.",
                model.AiModel);
        }
        catch (Exception ex)
        {
            return new TestAiResponse(false, $"Error al conectar con el endpoint de IA: {ex.Message}");
        }
    }

    public async Task<TestEmailResponse> TestEmailAsync(string targetEmail, CancellationToken ct = default)
    {
        PersistedSettingsModel model = await GetPersistedModelAsync(ct);
        if (!model.EmailNotificationsEnabled)
        {
            return new TestEmailResponse(false, "El envío de correos no está habilitado en la configuración.");
        }

        if (string.IsNullOrWhiteSpace(model.SmtpHost))
        {
            return new TestEmailResponse(false, "El host SMTP no está configurado.");
        }

        try
        {
            using var tcpClient = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            await tcpClient.ConnectAsync(model.SmtpHost, model.SmtpPort, cts.Token);

            return new TestEmailResponse(
                true,
                $"Conexión TCP establecida correctamente con el servidor SMTP {model.SmtpHost}:{model.SmtpPort}. Simulación a '{targetEmail}' exitosa.");
        }
        catch (Exception ex)
        {
            return new TestEmailResponse(
                false,
                $"Fallo de conexión con el servidor SMTP {model.SmtpHost}:{model.SmtpPort}: {ex.Message}");
        }
    }

    private async Task<PersistedSettingsModel> GetPersistedModelAsync(CancellationToken ct)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            _cached = await LoadFromFileOrDefaultsInternalAsync(ct);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<PersistedSettingsModel> LoadFromFileOrDefaultsInternalAsync(CancellationToken ct)
    {
        if (File.Exists(_filePath))
        {
            try
            {
                string json = await File.ReadAllTextAsync(_filePath, ct);
                PersistedSettingsModel? model = JsonSerializer.Deserialize<PersistedSettingsModel>(json, JsonOptions);
                if (model is not null)
                {
                    return model;
                }
            }
            catch (Exception ex)
            {
                InfrastructureSettingsLogMessages.FailedToReadSettings(_logger, _filePath, ex);
            }
        }

        return new PersistedSettingsModel();
    }

    private async Task SaveToFileAsync(PersistedSettingsModel model, CancellationToken ct)
    {
        string json = JsonSerializer.Serialize(model, JsonOptions);
        string tempFile = $"{_filePath}.tmp.{Guid.NewGuid():N}";
        await File.WriteAllTextAsync(tempFile, json, ct);
        File.Move(tempFile, _filePath, overwrite: true);
    }

    private SystemSettingsDto MapToDto(PersistedSettingsModel model)
    {
        string dbProvider = _configuration["Database:Provider"] ?? "Sqlite";
        string environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
        string storageRoot = _configuration["Storage:LocalRoot"] ?? Path.Combine(AppContext.BaseDirectory, "storage");
        string appVersion = "1.2.0";

        string uptimeStr = "0m";
        int activeThreads = 0;
        try
        {
            Process currentProcess = Process.GetCurrentProcess();
            TimeSpan uptime = DateTime.UtcNow - currentProcess.StartTime.ToUniversalTime();
            uptimeStr = uptime.Days > 0
                ? $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
                : $"{uptime.Hours}h {uptime.Minutes}m";
            activeThreads = currentProcess.Threads.Count;
        }
        catch
        {
            // Process inspection fallback
        }

        long memoryMb = GC.GetTotalMemory(false) / (1024 * 1024);

        long freeDiskMb = 0;
        try
        {
            string root = Path.GetPathRoot(Path.GetFullPath(_dataDir)) ?? "/";
            var drive = new DriveInfo(root);
            freeDiskMb = drive.AvailableFreeSpace / (1024 * 1024);
        }
        catch
        {
            // Ignore disk inspection errors in container sandbox
        }

        return new SystemSettingsDto(
            // 1. General, Brand & Whitelabel
            InstanceTitle: model.InstanceTitle,
            SupportEmail: model.SupportEmail,
            DefaultLanguage: model.DefaultLanguage,
            DefaultTheme: model.DefaultTheme,
            AllowPublicRegistration: model.AllowPublicRegistration,
            WelcomeMessage: model.WelcomeMessage,
            EnableKeyboardShortcuts: model.EnableKeyboardShortcuts,
            EnableDenseModeByDefault: model.EnableDenseModeByDefault,
            ShowCardCoverImagesByDefault: model.ShowCardCoverImagesByDefault,
            EnableSoundNotifications: model.EnableSoundNotifications,
            CustomLogoUrl: model.CustomLogoUrl,
            CustomFaviconUrl: model.CustomFaviconUrl,
            HidePoweredByCardscape: model.HidePoweredByCardscape,
            MaintenanceModeEnabled: model.MaintenanceModeEnabled,
            MaintenanceModeMessage: model.MaintenanceModeMessage,
            SystemAnnouncementEnabled: model.SystemAnnouncementEnabled,
            SystemAnnouncementMessage: model.SystemAnnouncementMessage,
            SystemAnnouncementType: model.SystemAnnouncementType,

            // 2. Security & Policy
            JwtAccessTokenMinutes: model.JwtAccessTokenMinutes,
            PasswordMinLength: model.PasswordMinLength,
            PasswordRequireDigit: model.PasswordRequireDigit,
            PasswordRequireNonAlphanumeric: model.PasswordRequireNonAlphanumeric,
            RequireTwoFactorForAdmins: model.RequireTwoFactorForAdmins,
            MaxFailedLoginAttempts: model.MaxFailedLoginAttempts,
            LockoutDurationMinutes: model.LockoutDurationMinutes,
            SingleActiveSessionPerUser: model.SingleActiveSessionPerUser,
            MaxApiTokensPerUser: model.MaxApiTokensPerUser,
            ApiTokenExpirationDays: model.ApiTokenExpirationDays,
            DefaultApiTokenExpiryDays: model.DefaultApiTokenExpiryDays,
            MaxApiTokenExpiryDays: model.MaxApiTokenExpiryDays,
            CacheAdminClaim: model.CacheAdminClaim,
            TotpIssuerName: model.TotpIssuerName,
            TotpStepTolerance: model.TotpStepTolerance,
            PasswordResetTokenLifetimeMinutes: model.PasswordResetTokenLifetimeMinutes,
            IdleSessionTimeoutMinutes: model.IdleSessionTimeoutMinutes,
            MaxConcurrentSessionsPerUser: model.MaxConcurrentSessionsPerUser,
            PasswordExpiryDays: model.PasswordExpiryDays,
            PasswordHistoryCount: model.PasswordHistoryCount,
            CorsAllowedOrigins: model.CorsAllowedOrigins,
            EnforceHttps: model.EnforceHttps,
            EnableSecurityHeaders: model.EnableSecurityHeaders,
            HstsMaxAgeSeconds: model.HstsMaxAgeSeconds,
            HstsIncludeSubdomains: model.HstsIncludeSubdomains,
            HstsPreload: model.HstsPreload,
            ContentSecurityPolicy: model.ContentSecurityPolicy,
            XFrameOptions: model.XFrameOptions,
            ReferrerPolicy: model.ReferrerPolicy,
            DataProtectionKeyLifetimeDays: model.DataProtectionKeyLifetimeDays,
            DataProtectionKeyDirectory: model.DataProtectionKeyDirectory,

            // 3. Enterprise SSO & Provisioning
            SamlSsoEnabled: model.SamlSsoEnabled,
            SamlEnforceForDomain: model.SamlEnforceForDomain,
            ScimProvisioningEnabled: model.ScimProvisioningEnabled,
            ScimTokenExpirationDays: model.ScimTokenExpirationDays,
            OAuthAppsEnabled: model.OAuthAppsEnabled,
            MaxOAuthAppsPerUser: model.MaxOAuthAppsPerUser,

            // 4. External / Social OAuth
            EnableGoogleAuth: model.EnableGoogleAuth,
            GoogleClientId: model.GoogleClientId,
            GoogleClientSecretMasked: string.IsNullOrWhiteSpace(model.GoogleClientSecret) ? "" : "******",
            EnableGitHubAuth: model.EnableGitHubAuth,
            GitHubClientId: model.GitHubClientId,
            GitHubClientSecretMasked: string.IsNullOrWhiteSpace(model.GitHubClientSecret) ? "" : "******",
            EnableMicrosoftAuth: model.EnableMicrosoftAuth,
            MicrosoftClientId: model.MicrosoftClientId,
            MicrosoftClientSecretMasked: string.IsNullOrWhiteSpace(model.MicrosoftClientSecret) ? "" : "******",
            EnableAppleAuth: model.EnableAppleAuth,
            AppleClientId: model.AppleClientId,
            AppleTeamId: model.AppleTeamId,
            AppleKeyId: model.AppleKeyId,
            ApplePrivateKeyPemMasked: string.IsNullOrWhiteSpace(model.ApplePrivateKeyPem) ? "" : "******",

            // 5. Workspaces & Boards
            MaxWorkspacesPerUser: model.MaxWorkspacesPerUser,
            MaxBoardsPerWorkspace: model.MaxBoardsPerWorkspace,
            MaxMembersPerWorkspace: model.MaxMembersPerWorkspace,
            DefaultWorkspaceRole: model.DefaultWorkspaceRole,
            InvitationExpirationDays: model.InvitationExpirationDays,
            AllowPublicBoards: model.AllowPublicBoards,
            EnablePublicBoardTemplates: model.EnablePublicBoardTemplates,
            AllowCustomUserTemplates: model.AllowCustomUserTemplates,

            // 6. Cards, Lists & Productivity
            DefaultWipLimit: model.DefaultWipLimit,
            EnforceWipLimits: model.EnforceWipLimits,
            HighlightOverLimitLists: model.HighlightOverLimitLists,
            AllowCardMirroring: model.AllowCardMirroring,
            AllowCardSnoozing: model.AllowCardSnoozing,
            AllowCardVoting: model.AllowCardVoting,
            MaxVotesPerUserPerCard: model.MaxVotesPerUserPerCard,
            MaxChecklistsPerCard: model.MaxChecklistsPerCard,
            AutoArchiveCompletedCardsDays: model.AutoArchiveCompletedCardsDays,
            CardRecurrenceEnabled: model.CardRecurrenceEnabled,
            MaxRecurrenceIntervalDays: model.MaxRecurrenceIntervalDays,
            EnableTimelineView: model.EnableTimelineView,
            EnableCalendarView: model.EnableCalendarView,
            EnableTableView: model.EnableTableView,
            DefaultBoardView: model.DefaultBoardView,

            // 7. Time Tracking & Estimates
            EnableTimeTracking: model.EnableTimeTracking,
            EnforceTimeTrackingEstimates: model.EnforceTimeTrackingEstimates,
            TimeTrackingUnit: model.TimeTrackingUnit,

            // 8. Comments & Collaboration
            AllowCommentEditing: model.AllowCommentEditing,
            AllowCommentDeletion: model.AllowCommentDeletion,
            MaxCommentLength: model.MaxCommentLength,
            AllowUserMentions: model.AllowUserMentions,

            // 9. Labels & Custom Fields
            MaxLabelsPerBoard: model.MaxLabelsPerBoard,
            MaxLabelsPerCard: model.MaxLabelsPerCard,
            CustomFieldsEnabled: model.CustomFieldsEnabled,
            MaxCustomFieldsPerBoard: model.MaxCustomFieldsPerBoard,

            // 10. Board Automation Rules
            BoardAutomationEnabled: model.BoardAutomationEnabled,
            MaxAutomationRulesPerBoard: model.MaxAutomationRulesPerBoard,
            MaxAutomationActionsPerRule: model.MaxAutomationActionsPerRule,
            AutomationMonthlyRunQuotaPerUser: model.AutomationMonthlyRunQuotaPerUser,
            AutomationTimeoutSeconds: model.AutomationTimeoutSeconds,

            // 11. Notifications & System Alerts
            InAppNotificationsEnabled: model.InAppNotificationsEnabled,
            DueSoonThresholdHours: model.DueSoonThresholdHours,
            NotifyOnCardAssignment: model.NotifyOnCardAssignment,
            NotifyOnCardMention: model.NotifyOnCardMention,
            NotifyOnDueSoon: model.NotifyOnDueSoon,
            NotifyOnOverdue: model.NotifyOnOverdue,
            NotificationRetentionDays: model.NotificationRetentionDays,
            EnableWebPushNotifications: model.EnableWebPushNotifications,
            VapidSubject: model.VapidSubject,
            VapidPublicKey: model.VapidPublicKey,
            VapidPrivateKeyMasked: string.IsNullOrWhiteSpace(model.VapidPrivateKey) ? "" : "******",

            // 12. Dashboards & Metric Cards
            EnableDashboards: model.EnableDashboards,
            MaxDashcardsPerBoard: model.MaxDashcardsPerBoard,
            DashboardRefreshIntervalSeconds: model.DashboardRefreshIntervalSeconds,

            // 13. Import & Export
            EnableBoardExport: model.EnableBoardExport,
            EnableKanbanImport: model.EnableKanbanImport,
            MaxImportFileSizeMb: model.MaxImportFileSizeMb,

            // 14. Search & Indexing
            SearchMinQueryLength: model.SearchMinQueryLength,
            SearchMaxPageSize: model.SearchMaxPageSize,
            SearchFuzzyMatching: model.SearchFuzzyMatching,

            // 15. Realtime & Presence
            RealtimeBroadcastingEnabled: model.RealtimeBroadcastingEnabled,
            RealtimePresenceEnabled: model.RealtimePresenceEnabled,

            // 16. Card Aging
            CardAgingEnabled: model.CardAgingEnabled,
            CardAgingInactiveDays: model.CardAgingInactiveDays,
            CardAgingMode: model.CardAgingMode,

            // 17. Storage & Attachments
            StorageProvider: "LocalFile",
            StorageRoot: storageRoot,
            MaxAttachmentSizeMb: model.MaxAttachmentSizeMb,
            AllowedAttachmentExtensions: model.AllowedAttachmentExtensions,
            AllowCoverImages: model.AllowCoverImages,
            MaxCoverImageSizeMb: model.MaxCoverImageSizeMb,
            S3BucketName: model.S3BucketName,
            S3EndpointUrl: model.S3EndpointUrl,
            S3Region: model.S3Region,
            S3AccessKey: model.S3AccessKey,
            S3SecretKeyMasked: string.IsNullOrWhiteSpace(model.S3SecretKey) ? "" : "******",
            S3ForcePathStyle: model.S3ForcePathStyle,
            BlockExecutableAttachments: model.BlockExecutableAttachments,
            ScanAttachmentsForMalware: model.ScanAttachmentsForMalware,
            ClamAvDaemonEndpoint: model.ClamAvDaemonEndpoint,

            // 18. Artificial Intelligence
            AiEnabled: model.AiEnabled,
            AiProvider: model.AiProvider,
            AiEndpoint: model.AiEndpoint,
            AiModel: model.AiModel,
            AiApiKeyMasked: string.IsNullOrWhiteSpace(model.AiApiKey) ? "" : "******",
            AiTimeoutSeconds: model.AiTimeoutSeconds,
            AiMaxTokens: model.AiMaxTokens,
            AiEnableCardDescriptionGen: model.AiEnableCardDescriptionGen,
            AiEnableCommentSummary: model.AiEnableCommentSummary,
            AiEnableAutoChecklists: model.AiEnableAutoChecklists,
            AiTemperature: model.AiTemperature,

            // 19. Outbound Email (SMTP)
            EmailNotificationsEnabled: model.EmailNotificationsEnabled,
            SmtpHost: model.SmtpHost,
            SmtpPort: model.SmtpPort,
            SmtpUsername: model.SmtpUsername,
            SmtpPasswordMasked: string.IsNullOrWhiteSpace(model.SmtpPassword) ? "" : "******",
            SmtpEnableSsl: model.SmtpEnableSsl,
            SenderEmail: model.SenderEmail,
            SenderName: model.SenderName,

            // 20. Inbound Email (Email-to-Board)
            InboundEmailEnabled: model.InboundEmailEnabled,
            InboundEmailDomain: model.InboundEmailDomain,
            InboundDefaultList: model.InboundDefaultList,
            InboundAttachSenderEmail: model.InboundAttachSenderEmail,

            // 21. Integrations (Slack, GitHub, Calendar, MCP, Cloud)
            SlackIntegrationEnabled: model.SlackIntegrationEnabled,
            SlackClientId: model.SlackClientId,
            SlackClientSecretMasked: string.IsNullOrWhiteSpace(model.SlackClientSecret) ? "" : "******",
            SlackSigningSecretMasked: string.IsNullOrWhiteSpace(model.SlackSigningSecret) ? "" : "******",
            SlackBotTokenMasked: string.IsNullOrWhiteSpace(model.SlackBotToken) ? "" : "******",
            GitHubIntegrationEnabled: model.GitHubIntegrationEnabled,
            GitHubTokenMasked: string.IsNullOrWhiteSpace(model.GitHubToken) ? "" : "******",
            GitHubSyncIntervalMinutes: model.GitHubSyncIntervalMinutes,
            GitHubAutoCloseCardsOnPrMerge: model.GitHubAutoCloseCardsOnPrMerge,
            GoogleCalendarIntegrationEnabled: model.GoogleCalendarIntegrationEnabled,
            GoogleCalendarClientId: model.GoogleCalendarClientId,
            GoogleCalendarClientSecretMasked: string.IsNullOrWhiteSpace(model.GoogleCalendarClientSecret) ? "" : "******",
            GoogleCalendarSyncIntervalMinutes: model.GoogleCalendarSyncIntervalMinutes,
            McpServerEnabled: model.McpServerEnabled,
            CalendarIcsFeedsEnabled: model.CalendarIcsFeedsEnabled,
            CalendarFeedTokenLifetimeDays: model.CalendarFeedTokenLifetimeDays,
            GoogleDriveIntegrationEnabled: model.GoogleDriveIntegrationEnabled,
            OneDriveIntegrationEnabled: model.OneDriveIntegrationEnabled,
            DropboxIntegrationEnabled: model.DropboxIntegrationEnabled,
            MicrosoftTeamsIntegrationEnabled: model.MicrosoftTeamsIntegrationEnabled,
            MicrosoftTeamsWebhookUrlMasked: string.IsNullOrWhiteSpace(model.MicrosoftTeamsWebhookUrl) ? "" : "******",
            DiscordIntegrationEnabled: model.DiscordIntegrationEnabled,
            DiscordWebhookUrlMasked: string.IsNullOrWhiteSpace(model.DiscordWebhookUrl) ? "" : "******",
            GitLabIntegrationEnabled: model.GitLabIntegrationEnabled,
            GitLabEndpoint: model.GitLabEndpoint,

            // 22. Model Context Protocol (MCP Server)
            McpServerName: model.McpServerName,
            McpEnableWriteTools: model.McpEnableWriteTools,
            McpMaxBatchSize: model.McpMaxBatchSize,

            // 23. Webhooks
            WebhooksEnabled: model.WebhooksEnabled,
            MaxWebhookRetries: model.MaxWebhookRetries,
            WebhookTimeoutSeconds: model.WebhookTimeoutSeconds,
            WebhookPayloadSignatureEnabled: model.WebhookPayloadSignatureEnabled,

            // 24. API Idempotency
            EnableIdempotency: model.EnableIdempotency,
            IdempotencyReservationWindowMinutes: model.IdempotencyReservationWindowMinutes,
            IdempotencyRetentionWindowHours: model.IdempotencyRetentionWindowHours,

            // 25. Rate Limiting & Performance
            RateLimitingEnabled: model.RateLimitingEnabled,
            DefaultRequestsPerHour: model.DefaultRequestsPerHour,
            RateLimiterBackend: model.RateLimiterBackend,
            BackgroundJobPollIntervalSeconds: model.BackgroundJobPollIntervalSeconds,
            BackgroundJobBatchSize: model.BackgroundJobBatchSize,

            // 26. Infrastructure, Redis & Observability
            RedisConnectionStringMasked: string.IsNullOrWhiteSpace(model.RedisConnectionString) ? "" : "******",
            RedisDatabase: model.RedisDatabase,
            PendingTotpStoreBackend: model.PendingTotpStoreBackend,
            PendingTotpStoreKeyPrefix: model.PendingTotpStoreKeyPrefix,
            RateLimiterKeyPrefix: model.RateLimiterKeyPrefix,
            OtelTracingEnabled: model.OtelTracingEnabled,
            OtelMetricsEnabled: model.OtelMetricsEnabled,
            OtelEndpointUrl: model.OtelEndpointUrl,
            OtelServiceName: model.OtelServiceName,
            OtelTraceSampleRate: model.OtelTraceSampleRate,
            OutboxProcessorEnabled: model.OutboxProcessorEnabled,
            OutboxBatchSize: model.OutboxBatchSize,
            OutboxPollIntervalSeconds: model.OutboxPollIntervalSeconds,
            OutboxMaxAttempts: model.OutboxMaxAttempts,
            OutboxMessageRetentionDays: model.OutboxMessageRetentionDays,

            // 27. Activities & Audit Logging
            EnableActivityLogging: model.EnableActivityLogging,
            ActivityPageSize: model.ActivityPageSize,
            AuditLogDetailedIp: model.AuditLogDetailedIp,

            // 28. System Logging (Serilog)
            LogRetainedFileCountLimit: model.LogRetainedFileCountLimit,
            LogFileSizeLimitMb: model.LogFileSizeLimitMb,

            // 29. Data Retention, Residency & GDPR
            RetentionSweeperEnabled: model.RetentionSweeperEnabled,
            SweepIntervalHours: model.SweepIntervalHours,
            UserGracePeriodDays: model.UserGracePeriodDays,
            ActivityRetentionDays: model.ActivityRetentionDays,
            AuditRetentionDays: model.AuditRetentionDays,
            AutoPurgeOrphanAttachments: model.AutoPurgeOrphanAttachments,
            DataResidencyEnabled: model.DataResidencyEnabled,
            DeploymentRegion: model.DeploymentRegion,
            EnforceDataResidency: model.EnforceDataResidency,
            SoftDeleteRetentionDays: model.SoftDeleteRetentionDays,
            PermanentDeleteRequiresAdmin: model.PermanentDeleteRequiresAdmin,

            // 30. Legal Notices, Privacy & Compliance
            CustomPrivacyPolicyUrl: model.CustomPrivacyPolicyUrl,
            CustomTermsOfServiceUrl: model.CustomTermsOfServiceUrl,
            DisplayCookieBanner: model.DisplayCookieBanner,
            RequireLegalNoticeAcceptance: model.RequireLegalNoticeAcceptance,

            // 31. Automated Backups
            AutoBackupEnabled: model.AutoBackupEnabled,
            BackupIntervalHours: model.BackupIntervalHours,
            BackupRetentionDays: model.BackupRetentionDays,

            // 32. Seeder & Dev
            SeederEnabled: model.SeederEnabled,
            AllowSeederExecution: model.AllowSeederExecution,
            SeederWipeBeforeSeed: model.SeederWipeBeforeSeed,

            // 33. Diagnostics
            DatabaseProvider: dbProvider,
            DatabaseHealth: "Healthy",
            Environment: environment,
            AppVersion: appVersion,
            Uptime: uptimeStr,
            MemoryUsageMb: memoryMb,
            FreeDiskSpaceMb: freeDiskMb,
            ActiveThreads: activeThreads);
    }

    public void Dispose()
    {
        _lock.Dispose();
    }

    private sealed class PersistedSettingsModel
    {
        // 1. General, Brand & Whitelabel
        public string InstanceTitle { get; set; } = "Cardscape";
        public string SupportEmail { get; set; } = "support@cardscape.local";
        public string DefaultLanguage { get; set; } = "es";
        public string DefaultTheme { get; set; } = "default";
        public bool AllowPublicRegistration { get; set; } = true;
        public string WelcomeMessage { get; set; } = "Bienvenido a Cardscape";
        public bool EnableKeyboardShortcuts { get; set; } = true;
        public bool EnableDenseModeByDefault { get; set; }
        public bool ShowCardCoverImagesByDefault { get; set; } = true;
        public bool EnableSoundNotifications { get; set; } = true;
        public string CustomLogoUrl { get; set; } = string.Empty;
        public string CustomFaviconUrl { get; set; } = string.Empty;
        public bool HidePoweredByCardscape { get; set; }
        public bool MaintenanceModeEnabled { get; set; }
        public string MaintenanceModeMessage { get; set; } = "El sistema se encuentra en mantenimiento programado.";
        public bool SystemAnnouncementEnabled { get; set; }
        public string SystemAnnouncementMessage { get; set; } = string.Empty;
        public string SystemAnnouncementType { get; set; } = "Info";

        // 2. Security & Policy
        public int JwtAccessTokenMinutes { get; set; } = 1440;
        public int PasswordMinLength { get; set; } = 8;
        public bool PasswordRequireDigit { get; set; } = true;
        public bool PasswordRequireNonAlphanumeric { get; set; }
        public bool RequireTwoFactorForAdmins { get; set; }
        public int MaxFailedLoginAttempts { get; set; } = 5;
        public int LockoutDurationMinutes { get; set; } = 15;
        public bool SingleActiveSessionPerUser { get; set; }
        public int MaxApiTokensPerUser { get; set; } = 10;
        public int ApiTokenExpirationDays { get; set; } = 90;
        public int DefaultApiTokenExpiryDays { get; set; } = 90;
        public int MaxApiTokenExpiryDays { get; set; } = 365;
        public bool CacheAdminClaim { get; set; } = true;
        public string TotpIssuerName { get; set; } = "Cardscape";
        public int TotpStepTolerance { get; set; } = 1;
        public int PasswordResetTokenLifetimeMinutes { get; set; } = 60;
        public int IdleSessionTimeoutMinutes { get; set; }
        public int MaxConcurrentSessionsPerUser { get; set; } = 5;
        public int PasswordExpiryDays { get; set; }
        public int PasswordHistoryCount { get; set; }
        public string CorsAllowedOrigins { get; set; } = "*";
        public bool EnforceHttps { get; set; } = true;
        public bool EnableSecurityHeaders { get; set; } = true;
        public int HstsMaxAgeSeconds { get; set; } = 31536000;
        public bool HstsIncludeSubdomains { get; set; } = true;
        public bool HstsPreload { get; set; }
        public string ContentSecurityPolicy { get; set; } = "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self' wss: https:;";
        public string XFrameOptions { get; set; } = "DENY";
        public string ReferrerPolicy { get; set; } = "no-referrer";
        public int DataProtectionKeyLifetimeDays { get; set; } = 90;
        public string DataProtectionKeyDirectory { get; set; } = string.Empty;

        // 3. Enterprise SSO & Provisioning
        public bool SamlSsoEnabled { get; set; }
        public string SamlEnforceForDomain { get; set; } = string.Empty;
        public bool ScimProvisioningEnabled { get; set; }
        public int ScimTokenExpirationDays { get; set; } = 180;
        public bool OAuthAppsEnabled { get; set; } = true;
        public int MaxOAuthAppsPerUser { get; set; } = 5;

        // 4. External / Social OAuth
        public bool EnableGoogleAuth { get; set; }
        public string GoogleClientId { get; set; } = string.Empty;
        public string GoogleClientSecret { get; set; } = string.Empty;
        public bool EnableGitHubAuth { get; set; }
        public string GitHubClientId { get; set; } = string.Empty;
        public string GitHubClientSecret { get; set; } = string.Empty;
        public bool EnableMicrosoftAuth { get; set; }
        public string MicrosoftClientId { get; set; } = string.Empty;
        public string MicrosoftClientSecret { get; set; } = string.Empty;
        public bool EnableAppleAuth { get; set; }
        public string AppleClientId { get; set; } = string.Empty;
        public string AppleTeamId { get; set; } = string.Empty;
        public string AppleKeyId { get; set; } = string.Empty;
        public string ApplePrivateKeyPem { get; set; } = string.Empty;

        // 5. Workspaces & Boards
        public int MaxWorkspacesPerUser { get; set; }
        public int MaxBoardsPerWorkspace { get; set; }
        public int MaxMembersPerWorkspace { get; set; }
        public string DefaultWorkspaceRole { get; set; } = "Member";
        public int InvitationExpirationDays { get; set; } = 7;
        public bool AllowPublicBoards { get; set; } = true;
        public bool EnablePublicBoardTemplates { get; set; } = true;
        public bool AllowCustomUserTemplates { get; set; } = true;

        // 6. Cards, Lists & Productivity
        public int DefaultWipLimit { get; set; }
        public bool EnforceWipLimits { get; set; }
        public bool HighlightOverLimitLists { get; set; } = true;
        public bool AllowCardMirroring { get; set; } = true;
        public bool AllowCardSnoozing { get; set; } = true;
        public bool AllowCardVoting { get; set; } = true;
        public int MaxVotesPerUserPerCard { get; set; } = 1;
        public int MaxChecklistsPerCard { get; set; } = 10;
        public int AutoArchiveCompletedCardsDays { get; set; }
        public bool CardRecurrenceEnabled { get; set; } = true;
        public int MaxRecurrenceIntervalDays { get; set; } = 365;
        public bool EnableTimelineView { get; set; } = true;
        public bool EnableCalendarView { get; set; } = true;
        public bool EnableTableView { get; set; } = true;
        public string DefaultBoardView { get; set; } = "Kanban";

        // 7. Time Tracking & Estimates
        public bool EnableTimeTracking { get; set; }
        public bool EnforceTimeTrackingEstimates { get; set; }
        public string TimeTrackingUnit { get; set; } = "Hours";

        // 8. Comments & Collaboration
        public bool AllowCommentEditing { get; set; } = true;
        public bool AllowCommentDeletion { get; set; } = true;
        public int MaxCommentLength { get; set; } = 5000;
        public bool AllowUserMentions { get; set; } = true;

        // 9. Labels & Custom Fields
        public int MaxLabelsPerBoard { get; set; } = 50;
        public int MaxLabelsPerCard { get; set; } = 10;
        public bool CustomFieldsEnabled { get; set; } = true;
        public int MaxCustomFieldsPerBoard { get; set; } = 30;

        // 10. Board Automation Rules
        public bool BoardAutomationEnabled { get; set; } = true;
        public int MaxAutomationRulesPerBoard { get; set; } = 20;
        public int MaxAutomationActionsPerRule { get; set; } = 5;
        public int AutomationMonthlyRunQuotaPerUser { get; set; } = 250;
        public int AutomationTimeoutSeconds { get; set; } = 15;

        // 11. Notifications & System Alerts
        public bool InAppNotificationsEnabled { get; set; } = true;
        public int DueSoonThresholdHours { get; set; } = 24;
        public bool NotifyOnCardAssignment { get; set; } = true;
        public bool NotifyOnCardMention { get; set; } = true;
        public bool NotifyOnDueSoon { get; set; } = true;
        public bool NotifyOnOverdue { get; set; } = true;
        public int NotificationRetentionDays { get; set; } = 30;
        public bool EnableWebPushNotifications { get; set; }
        public string VapidSubject { get; set; } = "mailto:admin@cardscape.local";
        public string VapidPublicKey { get; set; } = string.Empty;
        public string VapidPrivateKey { get; set; } = string.Empty;

        // 12. Dashboards & Metric Cards
        public bool EnableDashboards { get; set; } = true;
        public int MaxDashcardsPerBoard { get; set; } = 10;
        public int DashboardRefreshIntervalSeconds { get; set; } = 60;

        // 13. Import & Export
        public bool EnableBoardExport { get; set; } = true;
        public bool EnableKanbanImport { get; set; } = true;
        public int MaxImportFileSizeMb { get; set; } = 50;

        // 14. Search & Indexing
        public int SearchMinQueryLength { get; set; } = 2;
        public int SearchMaxPageSize { get; set; } = 50;
        public bool SearchFuzzyMatching { get; set; } = true;

        // 15. Realtime & Presence
        public bool RealtimeBroadcastingEnabled { get; set; } = true;
        public bool RealtimePresenceEnabled { get; set; } = true;

        // 16. Card Aging
        public bool CardAgingEnabled { get; set; } = true;
        public int CardAgingInactiveDays { get; set; } = 14;
        public string CardAgingMode { get; set; } = "Regular";

        // 17. Storage & Attachments
        public int MaxAttachmentSizeMb { get; set; } = 25;
        public string AllowedAttachmentExtensions { get; set; } = "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip";
        public bool AllowCoverImages { get; set; } = true;
        public int MaxCoverImageSizeMb { get; set; } = 5;
        public string S3BucketName { get; set; } = string.Empty;
        public string S3EndpointUrl { get; set; } = string.Empty;
        public string S3Region { get; set; } = "us-east-1";
        public string S3AccessKey { get; set; } = string.Empty;
        public string S3SecretKey { get; set; } = string.Empty;
        public bool S3ForcePathStyle { get; set; } = true;
        public bool BlockExecutableAttachments { get; set; } = true;
        public bool ScanAttachmentsForMalware { get; set; }
        public string ClamAvDaemonEndpoint { get; set; } = string.Empty;

        // 18. Artificial Intelligence
        public bool AiEnabled { get; set; }
        public string AiProvider { get; set; } = "OpenAiCompatible";
        public string AiEndpoint { get; set; } = "http://localhost:11434/";
        public string AiModel { get; set; } = "llama3.2";
        public string AiApiKey { get; set; } = string.Empty;
        public int AiTimeoutSeconds { get; set; } = 60;
        public int AiMaxTokens { get; set; } = 2048;
        public bool AiEnableCardDescriptionGen { get; set; } = true;
        public bool AiEnableCommentSummary { get; set; } = true;
        public bool AiEnableAutoChecklists { get; set; } = true;
        public int AiTemperature { get; set; } = 70;

        // 19. Outbound Email (SMTP)
        public bool EmailNotificationsEnabled { get; set; }
        public string SmtpHost { get; set; } = "localhost";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public bool SmtpEnableSsl { get; set; } = true;
        public string SenderEmail { get; set; } = "noreply@cardscape.local";
        public string SenderName { get; set; } = "Cardscape Notificaciones";

        // 20. Inbound Email (Email-to-Board)
        public bool InboundEmailEnabled { get; set; }
        public string InboundEmailDomain { get; set; } = "inbound.cardscape.local";
        public string InboundDefaultList { get; set; } = "Inbox";
        public bool InboundAttachSenderEmail { get; set; } = true;

        // 21. Integrations (Slack, GitHub, Calendar, MCP, Cloud)
        public bool SlackIntegrationEnabled { get; set; }
        public string SlackClientId { get; set; } = string.Empty;
        public string SlackClientSecret { get; set; } = string.Empty;
        public string SlackSigningSecret { get; set; } = string.Empty;
        public string SlackBotToken { get; set; } = string.Empty;
        public bool GitHubIntegrationEnabled { get; set; }
        public string GitHubToken { get; set; } = string.Empty;
        public int GitHubSyncIntervalMinutes { get; set; } = 15;
        public bool GitHubAutoCloseCardsOnPrMerge { get; set; } = true;
        public bool GoogleCalendarIntegrationEnabled { get; set; }
        public string GoogleCalendarClientId { get; set; } = string.Empty;
        public string GoogleCalendarClientSecret { get; set; } = string.Empty;
        public int GoogleCalendarSyncIntervalMinutes { get; set; } = 15;
        public bool McpServerEnabled { get; set; } = true;
        public bool CalendarIcsFeedsEnabled { get; set; } = true;
        public int CalendarFeedTokenLifetimeDays { get; set; } = 180;
        public bool GoogleDriveIntegrationEnabled { get; set; }
        public bool OneDriveIntegrationEnabled { get; set; }
        public bool DropboxIntegrationEnabled { get; set; }
        public bool MicrosoftTeamsIntegrationEnabled { get; set; }
        public string MicrosoftTeamsWebhookUrl { get; set; } = string.Empty;
        public bool DiscordIntegrationEnabled { get; set; }
        public string DiscordWebhookUrl { get; set; } = string.Empty;
        public bool GitLabIntegrationEnabled { get; set; }
        public string GitLabEndpoint { get; set; } = string.Empty;

        // 22. Model Context Protocol (MCP Server)
        public string McpServerName { get; set; } = "Cardscape-MCP";
        public bool McpEnableWriteTools { get; set; } = true;
        public int McpMaxBatchSize { get; set; } = 50;

        // 23. Webhooks
        public bool WebhooksEnabled { get; set; } = true;
        public int MaxWebhookRetries { get; set; } = 3;
        public int WebhookTimeoutSeconds { get; set; } = 10;
        public bool WebhookPayloadSignatureEnabled { get; set; } = true;

        // 24. API Idempotency
        public bool EnableIdempotency { get; set; } = true;
        public int IdempotencyReservationWindowMinutes { get; set; } = 15;
        public int IdempotencyRetentionWindowHours { get; set; } = 24;

        // 25. Rate Limiting & Performance
        public bool RateLimitingEnabled { get; set; } = true;
        public int DefaultRequestsPerHour { get; set; } = 1000;
        public string RateLimiterBackend { get; set; } = "InMemory";
        public int BackgroundJobPollIntervalSeconds { get; set; } = 2;
        public int BackgroundJobBatchSize { get; set; } = 10;

        // 26. Infrastructure, Redis & Observability
        public string RedisConnectionString { get; set; } = string.Empty;
        public int RedisDatabase { get; set; }
        public string PendingTotpStoreBackend { get; set; } = "InMemory";
        public string PendingTotpStoreKeyPrefix { get; set; } = "cardscape:totp-pending:";
        public string RateLimiterKeyPrefix { get; set; } = "cardscape:rl:";
        public bool OtelTracingEnabled { get; set; }
        public bool OtelMetricsEnabled { get; set; }
        public string OtelEndpointUrl { get; set; } = string.Empty;
        public string OtelServiceName { get; set; } = "Cardscape.Api";
        public int OtelTraceSampleRate { get; set; } = 100;
        public bool OutboxProcessorEnabled { get; set; } = true;
        public int OutboxBatchSize { get; set; } = 50;
        public int OutboxPollIntervalSeconds { get; set; } = 5;
        public int OutboxMaxAttempts { get; set; } = 5;
        public int OutboxMessageRetentionDays { get; set; } = 14;

        // 27. Activities & Audit Logging
        public bool EnableActivityLogging { get; set; } = true;
        public int ActivityPageSize { get; set; } = 25;
        public bool AuditLogDetailedIp { get; set; } = true;

        // 28. System Logging (Serilog)
        public int LogRetainedFileCountLimit { get; set; } = 30;
        public int LogFileSizeLimitMb { get; set; } = 100;

        // 29. Data Retention, Residency & GDPR
        public bool RetentionSweeperEnabled { get; set; } = true;
        public int SweepIntervalHours { get; set; } = 6;
        public int UserGracePeriodDays { get; set; } = 30;
        public int ActivityRetentionDays { get; set; } = 365;
        public int AuditRetentionDays { get; set; } = 730;
        public bool AutoPurgeOrphanAttachments { get; set; } = true;
        public bool DataResidencyEnabled { get; set; }
        public string DeploymentRegion { get; set; } = "Unspecified";
        public bool EnforceDataResidency { get; set; }
        public int SoftDeleteRetentionDays { get; set; } = 30;
        public bool PermanentDeleteRequiresAdmin { get; set; } = true;

        // 30. Legal Notices, Privacy & Compliance
        public string CustomPrivacyPolicyUrl { get; set; } = string.Empty;
        public string CustomTermsOfServiceUrl { get; set; } = string.Empty;
        public bool DisplayCookieBanner { get; set; }
        public bool RequireLegalNoticeAcceptance { get; set; }

        // 31. Automated Backups
        public bool AutoBackupEnabled { get; set; }
        public int BackupIntervalHours { get; set; } = 24;
        public int BackupRetentionDays { get; set; } = 30;

        // 32. Seeder & Dev
        public bool SeederEnabled { get; set; }
        public bool AllowSeederExecution { get; set; }
        public bool SeederWipeBeforeSeed { get; set; }
    }
}
