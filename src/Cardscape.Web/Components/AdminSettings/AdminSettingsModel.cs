using Cardscape.Web.Shared;

namespace Cardscape.Web.Components.AdminSettings;

public sealed class LanguageItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public static class AdminSettingsOptions
{
    public static readonly List<LanguageItem> Languages = new()
    {
        new LanguageItem { Code = "es", Name = "Español" },
        new LanguageItem { Code = "en", Name = "English" }
    };

    public static readonly List<string> Themes = new() { "default", "dark", "material", "humanistic" };
    public static readonly List<string> WorkspaceRoles = new() { "Member", "Guest", "Admin" };
    public static readonly List<string> BoardViews = new() { "Kanban", "Timeline", "Calendar", "Table" };
    public static readonly List<string> TimeUnits = new() { "Hours", "Minutes", "Days" };
    public static readonly List<string> AgingModes = new() { "Regular", "Pirate", "Fade" };
    public static readonly List<string> RateLimiterBackends = new() { "InMemory", "Redis" };
    public static readonly List<string> TotpBackends = new() { "InMemory", "Redis" };
    public static readonly List<string> Regions = new() { "Unspecified", "Europe", "NorthAmerica", "AsiaPacific", "SouthAmerica" };
    public static readonly List<string> AnnouncementTypes = new() { "Info", "Warning", "Critical" };
    public static readonly List<string> XFrameOptions = new() { "DENY", "SAMEORIGIN" };
    public static readonly List<string> ReferrerPolicies = new() { "no-referrer", "strict-origin-when-cross-origin", "origin" };
    public static readonly List<string> CompressionLevels = new() { "Optimal", "Fastest", "SmallestSize" };
}

public sealed class SettingsFormModel
{
    // 1. General & Brand
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
    public string? CustomLogoUrl { get; set; }
    public string? CustomFaviconUrl { get; set; }
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
    public int TotpCodeLength { get; set; } = 6;
    public int TotpRecoveryCodesCount { get; set; } = 10;
    public int TotpRecoveryCodeLength { get; set; } = 10;
    public int JwtRefreshTokenDays { get; set; } = 7;
    public string JwtIssuer { get; set; } = "Cardscape";
    public string JwtAudience { get; set; } = "CardscapeClient";
    public int PasswordResetTokenLifetimeMinutes { get; set; } = 60;
    public string CorsAllowedOrigins { get; set; } = "*";
    public bool EnforceHttps { get; set; } = true;
    public bool EnableSecurityHeaders { get; set; } = true;
    public int IdleSessionTimeoutMinutes { get; set; }
    public int MaxConcurrentSessionsPerUser { get; set; } = 5;
    public int PasswordExpiryDays { get; set; }
    public int PasswordHistoryCount { get; set; }
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
    public string? GoogleClientSecret { get; set; }
    public bool EnableGitHubAuth { get; set; }
    public string GitHubClientId { get; set; } = string.Empty;
    public string? GitHubClientSecret { get; set; }
    public bool EnableMicrosoftAuth { get; set; }
    public string MicrosoftClientId { get; set; } = string.Empty;
    public string? MicrosoftClientSecret { get; set; }
    public bool EnableAppleAuth { get; set; }
    public string AppleClientId { get; set; } = string.Empty;
    public string AppleTeamId { get; set; } = string.Empty;
    public string AppleKeyId { get; set; } = string.Empty;
    public string? ApplePrivateKeyPem { get; set; }

    // 5. Workspaces & Boards
    public int MaxWorkspacesPerUser { get; set; }
    public int MaxBoardsPerWorkspace { get; set; }
    public int MaxMembersPerWorkspace { get; set; }
    public string DefaultWorkspaceRole { get; set; } = "Member";
    public int InvitationExpirationDays { get; set; } = 7;
    public bool AllowPublicBoards { get; set; } = true;
    public bool EnablePublicBoardTemplates { get; set; } = true;
    public bool AllowCustomUserTemplates { get; set; } = true;
    public int MaxWorkspaceNameLength { get; set; } = 100;
    public int MaxBoardNameLength { get; set; } = 100;
    public int MaxBoardDescriptionLength { get; set; } = 2000;
    public int MaxListNameLength { get; set; } = 100;
    public int MaxDisplayNameLength { get; set; } = 80;

    // 6. Cards, Lists & Productivity
    public int DefaultWipLimit { get; set; }
    public bool EnforceWipLimits { get; set; }
    public bool HighlightOverLimitLists { get; set; } = true;
    public bool AllowCardMirroring { get; set; } = true;
    public bool AllowCardSnoozing { get; set; } = true;
    public bool AllowCardVoting { get; set; } = true;
    public int MaxVotesPerUserPerCard { get; set; } = 5;
    public int MaxChecklistsPerCard { get; set; } = 10;
    public int MaxCardTitleLength { get; set; } = 500;
    public int MaxCardDescriptionLength { get; set; } = 16000;
    public int MaxChecklistItemLength { get; set; } = 500;
    public int MaxAttachmentsPerCard { get; set; } = 50;
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
    public int MaxCommentLength { get; set; } = 2000;
    public bool AllowUserMentions { get; set; } = true;

    // 9. Labels & Custom Fields
    public int MaxLabelsPerBoard { get; set; } = 30;
    public int MaxLabelsPerCard { get; set; } = 10;
    public bool CustomFieldsEnabled { get; set; } = true;
    public int MaxCustomFieldsPerBoard { get; set; } = 20;

    // 10. Board Automation Rules
    public bool BoardAutomationEnabled { get; set; } = true;
    public int MaxAutomationRulesPerBoard { get; set; } = 15;
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
    public string? VapidPrivateKey { get; set; }

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
    public string AllowedAttachmentExtensions { get; set; } = "png,jpg,jpeg,gif,webp,pdf,docx,xlsx,txt,zip";
    public bool AllowCoverImages { get; set; } = true;
    public int MaxCoverImageSizeMb { get; set; } = 5;
    public string AllowedAvatarExtensions { get; set; } = "png,jpg,jpeg,webp";
    public int MaxAvatarSizeMb { get; set; } = 2;
    public string S3BucketName { get; set; } = string.Empty;
    public string S3EndpointUrl { get; set; } = string.Empty;
    public string S3Region { get; set; } = "us-east-1";
    public string S3AccessKey { get; set; } = string.Empty;
    public string? S3SecretKey { get; set; }
    public bool S3ForcePathStyle { get; set; } = true;
    public bool BlockExecutableAttachments { get; set; } = true;
    public bool ScanAttachmentsForMalware { get; set; }
    public string ClamAvDaemonEndpoint { get; set; } = string.Empty;

    // 18. Artificial Intelligence
    public bool AiEnabled { get; set; }
    public string AiProvider { get; set; } = "Ollama";
    public string AiEndpoint { get; set; } = "http://localhost:11434/";
    public string AiModel { get; set; } = "llama3:latest";
    public string? AiApiKey { get; set; }
    public int AiTimeoutSeconds { get; set; } = 30;
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
    public string? SmtpPassword { get; set; }
    public bool SmtpEnableSsl { get; set; } = true;
    public string SenderEmail { get; set; } = "noreply@cardscape.local";
    public string SenderName { get; set; } = "Cardscape Notificaciones";
    public int EmailRateLimitPerMinute { get; set; } = 60;
    public int EmailBatchSize { get; set; } = 20;
    public bool EmailIncludeUnsubscribeLink { get; set; }

    // 20. Inbound Email (Email-to-Board)
    public bool InboundEmailEnabled { get; set; }
    public string InboundEmailDomain { get; set; } = "inbound.cardscape.local";
    public string InboundDefaultList { get; set; } = "Inbox";
    public bool InboundAttachSenderEmail { get; set; } = true;

    // 21. Integrations (Slack, GitHub, Calendar, MCP, Cloud)
    public bool SlackIntegrationEnabled { get; set; }
    public string SlackClientId { get; set; } = string.Empty;
    public string? SlackClientSecret { get; set; }
    public string? SlackSigningSecret { get; set; }
    public string? SlackBotToken { get; set; }
    public bool GitHubIntegrationEnabled { get; set; }
    public string? GitHubToken { get; set; }
    public int GitHubSyncIntervalMinutes { get; set; } = 15;
    public bool GitHubAutoCloseCardsOnPrMerge { get; set; } = true;
    public bool GoogleCalendarIntegrationEnabled { get; set; }
    public string GoogleCalendarClientId { get; set; } = string.Empty;
    public string? GoogleCalendarClientSecret { get; set; }
    public int GoogleCalendarSyncIntervalMinutes { get; set; } = 15;
    public bool McpServerEnabled { get; set; } = true;
    public bool CalendarIcsFeedsEnabled { get; set; } = true;
    public int CalendarFeedTokenLifetimeDays { get; set; } = 180;
    public bool GoogleDriveIntegrationEnabled { get; set; }
    public bool OneDriveIntegrationEnabled { get; set; }
    public bool DropboxIntegrationEnabled { get; set; }
    public bool MicrosoftTeamsIntegrationEnabled { get; set; }
    public string? MicrosoftTeamsWebhookUrl { get; set; }
    public bool DiscordIntegrationEnabled { get; set; }
    public string? DiscordWebhookUrl { get; set; }
    public bool GitLabIntegrationEnabled { get; set; }
    public string GitLabEndpoint { get; set; } = "https://gitlab.com";

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
    public int DefaultPageSize { get; set; } = 50;
    public int MaxPageSize { get; set; } = 200;
    public int ActivityFeedPageSize { get; set; } = 50;

    // 26. Infrastructure & Redis
    public string? RedisConnectionString { get; set; }
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
    public bool ResponseCompressionEnabled { get; set; } = true;
    public string ResponseCompressionLevel { get; set; } = "Optimal";
    public int StaticFilesMaxAgeSeconds { get; set; } = 86400;
    public bool ForwardedHeadersEnabled { get; set; } = true;
    public int MaxRequestBodySizeMb { get; set; } = 30;
    public int DatabaseCommandTimeoutSeconds { get; set; } = 30;
    public int DatabaseMaxRetryCount { get; set; } = 3;
    public int DatabaseMaxRetryDelaySeconds { get; set; } = 5;
    public bool DatabaseEnableDetailedErrors { get; set; }
    public bool DatabaseEnableSensitiveDataLogging { get; set; }
    public bool RunMigrationsOnStartup { get; set; } = true;
    public bool EnableSwaggerInProduction { get; set; }

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
    public string? CustomPrivacyPolicyUrl { get; set; }
    public string? CustomTermsOfServiceUrl { get; set; }
    public bool DisplayCookieBanner { get; set; }
    public bool RequireLegalNoticeAcceptance { get; set; }

    // 30. Automated Backups
    public bool AutoBackupEnabled { get; set; }
    public int BackupIntervalHours { get; set; } = 24;
    public int BackupRetentionDays { get; set; } = 7;

    // 31. Seeder & Dev
    public bool SeederEnabled { get; set; }
    public bool AllowSeederExecution { get; set; }
    public bool SeederWipeBeforeSeed { get; set; }

    public void ApplyDto(SystemSettingsDto dto)
    {
        // 1. General & Brand
        InstanceTitle = dto.InstanceTitle;
        SupportEmail = dto.SupportEmail;
        DefaultLanguage = dto.DefaultLanguage;
        DefaultTheme = dto.DefaultTheme;
        AllowPublicRegistration = dto.AllowPublicRegistration;
        WelcomeMessage = dto.WelcomeMessage;
        EnableKeyboardShortcuts = dto.EnableKeyboardShortcuts;
        EnableDenseModeByDefault = dto.EnableDenseModeByDefault;
        ShowCardCoverImagesByDefault = dto.ShowCardCoverImagesByDefault;
        EnableSoundNotifications = dto.EnableSoundNotifications;
        CustomLogoUrl = dto.CustomLogoUrl;
        CustomFaviconUrl = dto.CustomFaviconUrl;
        HidePoweredByCardscape = dto.HidePoweredByCardscape;
        MaintenanceModeEnabled = dto.MaintenanceModeEnabled;
        MaintenanceModeMessage = dto.MaintenanceModeMessage;
        SystemAnnouncementEnabled = dto.SystemAnnouncementEnabled;
        SystemAnnouncementMessage = dto.SystemAnnouncementMessage;
        SystemAnnouncementType = dto.SystemAnnouncementType;

        // 2. Security & Policy
        JwtAccessTokenMinutes = dto.JwtAccessTokenMinutes;
        PasswordMinLength = dto.PasswordMinLength;
        PasswordRequireDigit = dto.PasswordRequireDigit;
        PasswordRequireNonAlphanumeric = dto.PasswordRequireNonAlphanumeric;
        RequireTwoFactorForAdmins = dto.RequireTwoFactorForAdmins;
        MaxFailedLoginAttempts = dto.MaxFailedLoginAttempts;
        LockoutDurationMinutes = dto.LockoutDurationMinutes;
        SingleActiveSessionPerUser = dto.SingleActiveSessionPerUser;
        MaxApiTokensPerUser = dto.MaxApiTokensPerUser;
        ApiTokenExpirationDays = dto.ApiTokenExpirationDays;
        DefaultApiTokenExpiryDays = dto.DefaultApiTokenExpiryDays;
        MaxApiTokenExpiryDays = dto.MaxApiTokenExpiryDays;
        CacheAdminClaim = dto.CacheAdminClaim;
        TotpIssuerName = dto.TotpIssuerName;
        TotpStepTolerance = dto.TotpStepTolerance;
        TotpCodeLength = dto.TotpCodeLength;
        TotpRecoveryCodesCount = dto.TotpRecoveryCodesCount;
        TotpRecoveryCodeLength = dto.TotpRecoveryCodeLength;
        JwtRefreshTokenDays = dto.JwtRefreshTokenDays;
        JwtIssuer = dto.JwtIssuer;
        JwtAudience = dto.JwtAudience;
        PasswordResetTokenLifetimeMinutes = dto.PasswordResetTokenLifetimeMinutes;
        IdleSessionTimeoutMinutes = dto.IdleSessionTimeoutMinutes;
        MaxConcurrentSessionsPerUser = dto.MaxConcurrentSessionsPerUser;
        PasswordExpiryDays = dto.PasswordExpiryDays;
        PasswordHistoryCount = dto.PasswordHistoryCount;
        CorsAllowedOrigins = dto.CorsAllowedOrigins;
        EnforceHttps = dto.EnforceHttps;
        EnableSecurityHeaders = dto.EnableSecurityHeaders;
        HstsMaxAgeSeconds = dto.HstsMaxAgeSeconds;
        HstsIncludeSubdomains = dto.HstsIncludeSubdomains;
        HstsPreload = dto.HstsPreload;
        ContentSecurityPolicy = dto.ContentSecurityPolicy;
        XFrameOptions = dto.XFrameOptions;
        ReferrerPolicy = dto.ReferrerPolicy;
        DataProtectionKeyLifetimeDays = dto.DataProtectionKeyLifetimeDays;
        DataProtectionKeyDirectory = dto.DataProtectionKeyDirectory;

        // 3. Enterprise SSO & Provisioning
        SamlSsoEnabled = dto.SamlSsoEnabled;
        SamlEnforceForDomain = dto.SamlEnforceForDomain;
        ScimProvisioningEnabled = dto.ScimProvisioningEnabled;
        ScimTokenExpirationDays = dto.ScimTokenExpirationDays;
        OAuthAppsEnabled = dto.OAuthAppsEnabled;
        MaxOAuthAppsPerUser = dto.MaxOAuthAppsPerUser;

        // 4. External / Social OAuth
        EnableGoogleAuth = dto.EnableGoogleAuth;
        GoogleClientId = dto.GoogleClientId;
        GoogleClientSecret = null;
        EnableGitHubAuth = dto.EnableGitHubAuth;
        GitHubClientId = dto.GitHubClientId;
        GitHubClientSecret = null;
        EnableMicrosoftAuth = dto.EnableMicrosoftAuth;
        MicrosoftClientId = dto.MicrosoftClientId;
        MicrosoftClientSecret = null;
        EnableAppleAuth = dto.EnableAppleAuth;
        AppleClientId = dto.AppleClientId;
        AppleTeamId = dto.AppleTeamId;
        AppleKeyId = dto.AppleKeyId;
        ApplePrivateKeyPem = null;

        // 5. Workspaces & Boards
        MaxWorkspacesPerUser = dto.MaxWorkspacesPerUser;
        MaxBoardsPerWorkspace = dto.MaxBoardsPerWorkspace;
        MaxMembersPerWorkspace = dto.MaxMembersPerWorkspace;
        DefaultWorkspaceRole = dto.DefaultWorkspaceRole;
        InvitationExpirationDays = dto.InvitationExpirationDays;
        AllowPublicBoards = dto.AllowPublicBoards;
        EnablePublicBoardTemplates = dto.EnablePublicBoardTemplates;
        AllowCustomUserTemplates = dto.AllowCustomUserTemplates;
        MaxWorkspaceNameLength = dto.MaxWorkspaceNameLength;
        MaxBoardNameLength = dto.MaxBoardNameLength;
        MaxBoardDescriptionLength = dto.MaxBoardDescriptionLength;
        MaxListNameLength = dto.MaxListNameLength;
        MaxDisplayNameLength = dto.MaxDisplayNameLength;

        // 6. Cards, Lists & Productivity
        DefaultWipLimit = dto.DefaultWipLimit;
        EnforceWipLimits = dto.EnforceWipLimits;
        HighlightOverLimitLists = dto.HighlightOverLimitLists;
        AllowCardMirroring = dto.AllowCardMirroring;
        AllowCardSnoozing = dto.AllowCardSnoozing;
        AllowCardVoting = dto.AllowCardVoting;
        MaxVotesPerUserPerCard = dto.MaxVotesPerUserPerCard;
        MaxChecklistsPerCard = dto.MaxChecklistsPerCard;
        MaxCardTitleLength = dto.MaxCardTitleLength;
        MaxCardDescriptionLength = dto.MaxCardDescriptionLength;
        MaxChecklistItemLength = dto.MaxChecklistItemLength;
        MaxAttachmentsPerCard = dto.MaxAttachmentsPerCard;
        AutoArchiveCompletedCardsDays = dto.AutoArchiveCompletedCardsDays;
        CardRecurrenceEnabled = dto.CardRecurrenceEnabled;
        MaxRecurrenceIntervalDays = dto.MaxRecurrenceIntervalDays;
        EnableTimelineView = dto.EnableTimelineView;
        EnableCalendarView = dto.EnableCalendarView;
        EnableTableView = dto.EnableTableView;
        DefaultBoardView = dto.DefaultBoardView;

        // 7. Time Tracking & Estimates
        EnableTimeTracking = dto.EnableTimeTracking;
        EnforceTimeTrackingEstimates = dto.EnforceTimeTrackingEstimates;
        TimeTrackingUnit = dto.TimeTrackingUnit;

        // 8. Comments & Collaboration
        AllowCommentEditing = dto.AllowCommentEditing;
        AllowCommentDeletion = dto.AllowCommentDeletion;
        MaxCommentLength = dto.MaxCommentLength;
        AllowUserMentions = dto.AllowUserMentions;

        // 9. Labels & Custom Fields
        MaxLabelsPerBoard = dto.MaxLabelsPerBoard;
        MaxLabelsPerCard = dto.MaxLabelsPerCard;
        CustomFieldsEnabled = dto.CustomFieldsEnabled;
        MaxCustomFieldsPerBoard = dto.MaxCustomFieldsPerBoard;

        // 10. Board Automation Rules
        BoardAutomationEnabled = dto.BoardAutomationEnabled;
        MaxAutomationRulesPerBoard = dto.MaxAutomationRulesPerBoard;
        MaxAutomationActionsPerRule = dto.MaxAutomationActionsPerRule;
        AutomationMonthlyRunQuotaPerUser = dto.AutomationMonthlyRunQuotaPerUser;
        AutomationTimeoutSeconds = dto.AutomationTimeoutSeconds;

        // 11. Notifications & System Alerts
        InAppNotificationsEnabled = dto.InAppNotificationsEnabled;
        DueSoonThresholdHours = dto.DueSoonThresholdHours;
        NotifyOnCardAssignment = dto.NotifyOnCardAssignment;
        NotifyOnCardMention = dto.NotifyOnCardMention;
        NotifyOnDueSoon = dto.NotifyOnDueSoon;
        NotifyOnOverdue = dto.NotifyOnOverdue;
        NotificationRetentionDays = dto.NotificationRetentionDays;
        EnableWebPushNotifications = dto.EnableWebPushNotifications;
        VapidSubject = dto.VapidSubject;
        VapidPublicKey = dto.VapidPublicKey;
        VapidPrivateKey = null;

        // 12. Dashboards & Metric Cards
        EnableDashboards = dto.EnableDashboards;
        MaxDashcardsPerBoard = dto.MaxDashcardsPerBoard;
        DashboardRefreshIntervalSeconds = dto.DashboardRefreshIntervalSeconds;

        // 13. Import & Export
        EnableBoardExport = dto.EnableBoardExport;
        EnableKanbanImport = dto.EnableKanbanImport;
        MaxImportFileSizeMb = dto.MaxImportFileSizeMb;

        // 14. Search & Indexing
        SearchMinQueryLength = dto.SearchMinQueryLength;
        SearchMaxPageSize = dto.SearchMaxPageSize;
        SearchFuzzyMatching = dto.SearchFuzzyMatching;

        // 15. Realtime & Presence
        RealtimeBroadcastingEnabled = dto.RealtimeBroadcastingEnabled;
        RealtimePresenceEnabled = dto.RealtimePresenceEnabled;

        // 16. Card Aging
        CardAgingEnabled = dto.CardAgingEnabled;
        CardAgingInactiveDays = dto.CardAgingInactiveDays;
        CardAgingMode = dto.CardAgingMode;

        // 17. Storage & Attachments
        MaxAttachmentSizeMb = dto.MaxAttachmentSizeMb;
        AllowedAttachmentExtensions = dto.AllowedAttachmentExtensions;
        AllowCoverImages = dto.AllowCoverImages;
        MaxCoverImageSizeMb = dto.MaxCoverImageSizeMb;
        AllowedAvatarExtensions = dto.AllowedAvatarExtensions;
        MaxAvatarSizeMb = dto.MaxAvatarSizeMb;
        S3BucketName = dto.S3BucketName;
        S3EndpointUrl = dto.S3EndpointUrl;
        S3Region = dto.S3Region;
        S3AccessKey = dto.S3AccessKey;
        S3SecretKey = null;
        S3ForcePathStyle = dto.S3ForcePathStyle;
        BlockExecutableAttachments = dto.BlockExecutableAttachments;
        ScanAttachmentsForMalware = dto.ScanAttachmentsForMalware;
        ClamAvDaemonEndpoint = dto.ClamAvDaemonEndpoint;

        // 18. Artificial Intelligence
        AiEnabled = dto.AiEnabled;
        AiProvider = dto.AiProvider;
        AiEndpoint = dto.AiEndpoint;
        AiModel = dto.AiModel;
        AiApiKey = null;
        AiTimeoutSeconds = dto.AiTimeoutSeconds;
        AiMaxTokens = dto.AiMaxTokens;
        AiEnableCardDescriptionGen = dto.AiEnableCardDescriptionGen;
        AiEnableCommentSummary = dto.AiEnableCommentSummary;
        AiEnableAutoChecklists = dto.AiEnableAutoChecklists;
        AiTemperature = dto.AiTemperature;

        // 19. Outbound Email (SMTP)
        EmailNotificationsEnabled = dto.EmailNotificationsEnabled;
        SmtpHost = dto.SmtpHost;
        SmtpPort = dto.SmtpPort;
        SmtpUsername = dto.SmtpUsername;
        SmtpPassword = null;
        SmtpEnableSsl = dto.SmtpEnableSsl;
        SenderEmail = dto.SenderEmail;
        SenderName = dto.SenderName;
        EmailRateLimitPerMinute = dto.EmailRateLimitPerMinute;
        EmailBatchSize = dto.EmailBatchSize;
        EmailIncludeUnsubscribeLink = dto.EmailIncludeUnsubscribeLink;

        // 20. Inbound Email (Email-to-Board)
        InboundEmailEnabled = dto.InboundEmailEnabled;
        InboundEmailDomain = dto.InboundEmailDomain;
        InboundDefaultList = dto.InboundDefaultList;
        InboundAttachSenderEmail = dto.InboundAttachSenderEmail;

        // 21. Integrations (Slack, GitHub, Calendar, MCP, Cloud)
        SlackIntegrationEnabled = dto.SlackIntegrationEnabled;
        SlackClientId = dto.SlackClientId;
        SlackClientSecret = null;
        SlackSigningSecret = null;
        SlackBotToken = null;
        GitHubIntegrationEnabled = dto.GitHubIntegrationEnabled;
        GitHubToken = null;
        GitHubSyncIntervalMinutes = dto.GitHubSyncIntervalMinutes;
        GitHubAutoCloseCardsOnPrMerge = dto.GitHubAutoCloseCardsOnPrMerge;
        GoogleCalendarIntegrationEnabled = dto.GoogleCalendarIntegrationEnabled;
        GoogleCalendarClientId = dto.GoogleCalendarClientId;
        GoogleCalendarClientSecret = null;
        GoogleCalendarSyncIntervalMinutes = dto.GoogleCalendarSyncIntervalMinutes;
        McpServerEnabled = dto.McpServerEnabled;
        CalendarIcsFeedsEnabled = dto.CalendarIcsFeedsEnabled;
        CalendarFeedTokenLifetimeDays = dto.CalendarFeedTokenLifetimeDays;
        GoogleDriveIntegrationEnabled = dto.GoogleDriveIntegrationEnabled;
        OneDriveIntegrationEnabled = dto.OneDriveIntegrationEnabled;
        DropboxIntegrationEnabled = dto.DropboxIntegrationEnabled;
        MicrosoftTeamsIntegrationEnabled = dto.MicrosoftTeamsIntegrationEnabled;
        MicrosoftTeamsWebhookUrl = null;
        DiscordIntegrationEnabled = dto.DiscordIntegrationEnabled;
        DiscordWebhookUrl = null;
        GitLabIntegrationEnabled = dto.GitLabIntegrationEnabled;
        GitLabEndpoint = dto.GitLabEndpoint;

        // 22. Model Context Protocol (MCP Server)
        McpServerName = dto.McpServerName;
        McpEnableWriteTools = dto.McpEnableWriteTools;
        McpMaxBatchSize = dto.McpMaxBatchSize;

        // 23. Webhooks
        WebhooksEnabled = dto.WebhooksEnabled;
        MaxWebhookRetries = dto.MaxWebhookRetries;
        WebhookTimeoutSeconds = dto.WebhookTimeoutSeconds;
        WebhookPayloadSignatureEnabled = dto.WebhookPayloadSignatureEnabled;

        // 24. API Idempotency
        EnableIdempotency = dto.EnableIdempotency;
        IdempotencyReservationWindowMinutes = dto.IdempotencyReservationWindowMinutes;
        IdempotencyRetentionWindowHours = dto.IdempotencyRetentionWindowHours;

        // 25. Rate Limiting & Performance
        RateLimitingEnabled = dto.RateLimitingEnabled;
        DefaultRequestsPerHour = dto.DefaultRequestsPerHour;
        RateLimiterBackend = dto.RateLimiterBackend;
        BackgroundJobPollIntervalSeconds = dto.BackgroundJobPollIntervalSeconds;
        BackgroundJobBatchSize = dto.BackgroundJobBatchSize;
        DefaultPageSize = dto.DefaultPageSize;
        MaxPageSize = dto.MaxPageSize;
        ActivityFeedPageSize = dto.ActivityFeedPageSize;

        // 26. Infrastructure & Redis
        RedisConnectionString = null;
        RedisDatabase = dto.RedisDatabase;
        PendingTotpStoreBackend = dto.PendingTotpStoreBackend;
        PendingTotpStoreKeyPrefix = dto.PendingTotpStoreKeyPrefix;
        RateLimiterKeyPrefix = dto.RateLimiterKeyPrefix;
        OtelTracingEnabled = dto.OtelTracingEnabled;
        OtelMetricsEnabled = dto.OtelMetricsEnabled;
        OtelEndpointUrl = dto.OtelEndpointUrl;
        OtelServiceName = dto.OtelServiceName;
        OtelTraceSampleRate = dto.OtelTraceSampleRate;
        OutboxProcessorEnabled = dto.OutboxProcessorEnabled;
        OutboxBatchSize = dto.OutboxBatchSize;
        OutboxPollIntervalSeconds = dto.OutboxPollIntervalSeconds;
        OutboxMaxAttempts = dto.OutboxMaxAttempts;
        OutboxMessageRetentionDays = dto.OutboxMessageRetentionDays;
        ResponseCompressionEnabled = dto.ResponseCompressionEnabled;
        ResponseCompressionLevel = dto.ResponseCompressionLevel;
        StaticFilesMaxAgeSeconds = dto.StaticFilesMaxAgeSeconds;
        ForwardedHeadersEnabled = dto.ForwardedHeadersEnabled;
        MaxRequestBodySizeMb = dto.MaxRequestBodySizeMb;
        DatabaseCommandTimeoutSeconds = dto.DatabaseCommandTimeoutSeconds;
        DatabaseMaxRetryCount = dto.DatabaseMaxRetryCount;
        DatabaseMaxRetryDelaySeconds = dto.DatabaseMaxRetryDelaySeconds;
        DatabaseEnableDetailedErrors = dto.DatabaseEnableDetailedErrors;
        DatabaseEnableSensitiveDataLogging = dto.DatabaseEnableSensitiveDataLogging;
        RunMigrationsOnStartup = dto.RunMigrationsOnStartup;
        EnableSwaggerInProduction = dto.EnableSwaggerInProduction;

        // 27. Activities & Audit Logging
        EnableActivityLogging = dto.EnableActivityLogging;
        ActivityPageSize = dto.ActivityPageSize;
        AuditLogDetailedIp = dto.AuditLogDetailedIp;

        // 28. System Logging (Serilog)
        LogRetainedFileCountLimit = dto.LogRetainedFileCountLimit;
        LogFileSizeLimitMb = dto.LogFileSizeLimitMb;

        // 29. Data Retention, Residency & GDPR
        RetentionSweeperEnabled = dto.RetentionSweeperEnabled;
        SweepIntervalHours = dto.SweepIntervalHours;
        UserGracePeriodDays = dto.UserGracePeriodDays;
        ActivityRetentionDays = dto.ActivityRetentionDays;
        AuditRetentionDays = dto.AuditRetentionDays;
        AutoPurgeOrphanAttachments = dto.AutoPurgeOrphanAttachments;
        DataResidencyEnabled = dto.DataResidencyEnabled;
        DeploymentRegion = dto.DeploymentRegion;
        EnforceDataResidency = dto.EnforceDataResidency;
        SoftDeleteRetentionDays = dto.SoftDeleteRetentionDays;
        PermanentDeleteRequiresAdmin = dto.PermanentDeleteRequiresAdmin;
        CustomPrivacyPolicyUrl = dto.CustomPrivacyPolicyUrl;
        CustomTermsOfServiceUrl = dto.CustomTermsOfServiceUrl;
        DisplayCookieBanner = dto.DisplayCookieBanner;
        RequireLegalNoticeAcceptance = dto.RequireLegalNoticeAcceptance;

        // 30. Automated Backups
        AutoBackupEnabled = dto.AutoBackupEnabled;
        BackupIntervalHours = dto.BackupIntervalHours;
        BackupRetentionDays = dto.BackupRetentionDays;

        // 31. Seeder & Dev
        SeederEnabled = dto.SeederEnabled;
        AllowSeederExecution = dto.AllowSeederExecution;
        SeederWipeBeforeSeed = dto.SeederWipeBeforeSeed;
    }

    public UpdateSystemSettingsRequestDto ToUpdateRequest() => new(
        // 1. General & Brand
        InstanceTitle: InstanceTitle,
        SupportEmail: SupportEmail,
        DefaultLanguage: DefaultLanguage,
        DefaultTheme: DefaultTheme,
        AllowPublicRegistration: AllowPublicRegistration,
        WelcomeMessage: WelcomeMessage,
        EnableKeyboardShortcuts: EnableKeyboardShortcuts,
        EnableDenseModeByDefault: EnableDenseModeByDefault,
        ShowCardCoverImagesByDefault: ShowCardCoverImagesByDefault,
        EnableSoundNotifications: EnableSoundNotifications,
        CustomLogoUrl: CustomLogoUrl ?? string.Empty,
        CustomFaviconUrl: CustomFaviconUrl ?? string.Empty,
        HidePoweredByCardscape: HidePoweredByCardscape,
        MaintenanceModeEnabled: MaintenanceModeEnabled,
        MaintenanceModeMessage: MaintenanceModeMessage,
        SystemAnnouncementEnabled: SystemAnnouncementEnabled,
        SystemAnnouncementMessage: SystemAnnouncementMessage,
        SystemAnnouncementType: SystemAnnouncementType,

        // 2. Security & Policy
        JwtAccessTokenMinutes: JwtAccessTokenMinutes,
        PasswordMinLength: PasswordMinLength,
        PasswordRequireDigit: PasswordRequireDigit,
        PasswordRequireNonAlphanumeric: PasswordRequireNonAlphanumeric,
        RequireTwoFactorForAdmins: RequireTwoFactorForAdmins,
        MaxFailedLoginAttempts: MaxFailedLoginAttempts,
        LockoutDurationMinutes: LockoutDurationMinutes,
        SingleActiveSessionPerUser: SingleActiveSessionPerUser,
        MaxApiTokensPerUser: MaxApiTokensPerUser,
        ApiTokenExpirationDays: ApiTokenExpirationDays,
        DefaultApiTokenExpiryDays: DefaultApiTokenExpiryDays,
        MaxApiTokenExpiryDays: MaxApiTokenExpiryDays,
        CacheAdminClaim: CacheAdminClaim,
        TotpIssuerName: TotpIssuerName,
        TotpStepTolerance: TotpStepTolerance,
        TotpCodeLength: TotpCodeLength,
        TotpRecoveryCodesCount: TotpRecoveryCodesCount,
        TotpRecoveryCodeLength: TotpRecoveryCodeLength,
        JwtRefreshTokenDays: JwtRefreshTokenDays,
        JwtIssuer: JwtIssuer,
        JwtAudience: JwtAudience,
        PasswordResetTokenLifetimeMinutes: PasswordResetTokenLifetimeMinutes,
        IdleSessionTimeoutMinutes: IdleSessionTimeoutMinutes,
        MaxConcurrentSessionsPerUser: MaxConcurrentSessionsPerUser,
        PasswordExpiryDays: PasswordExpiryDays,
        PasswordHistoryCount: PasswordHistoryCount,
        CorsAllowedOrigins: CorsAllowedOrigins,
        EnforceHttps: EnforceHttps,
        EnableSecurityHeaders: EnableSecurityHeaders,
        HstsMaxAgeSeconds: HstsMaxAgeSeconds,
        HstsIncludeSubdomains: HstsIncludeSubdomains,
        HstsPreload: HstsPreload,
        ContentSecurityPolicy: ContentSecurityPolicy,
        XFrameOptions: XFrameOptions,
        ReferrerPolicy: ReferrerPolicy,
        DataProtectionKeyLifetimeDays: DataProtectionKeyLifetimeDays,
        DataProtectionKeyDirectory: DataProtectionKeyDirectory,

        // 3. Enterprise SSO & Provisioning
        SamlSsoEnabled: SamlSsoEnabled,
        SamlEnforceForDomain: SamlEnforceForDomain,
        ScimProvisioningEnabled: ScimProvisioningEnabled,
        ScimTokenExpirationDays: ScimTokenExpirationDays,
        OAuthAppsEnabled: OAuthAppsEnabled,
        MaxOAuthAppsPerUser: MaxOAuthAppsPerUser,

        // 4. External / Social OAuth
        EnableGoogleAuth: EnableGoogleAuth,
        GoogleClientId: GoogleClientId,
        GoogleClientSecret: GoogleClientSecret,
        EnableGitHubAuth: EnableGitHubAuth,
        GitHubClientId: GitHubClientId,
        GitHubClientSecret: GitHubClientSecret,
        EnableMicrosoftAuth: EnableMicrosoftAuth,
        MicrosoftClientId: MicrosoftClientId,
        MicrosoftClientSecret: MicrosoftClientSecret,
        EnableAppleAuth: EnableAppleAuth,
        AppleClientId: AppleClientId,
        AppleTeamId: AppleTeamId,
        AppleKeyId: AppleKeyId,
        ApplePrivateKeyPem: ApplePrivateKeyPem,

        // 5. Workspaces & Boards
        MaxWorkspacesPerUser: MaxWorkspacesPerUser,
        MaxBoardsPerWorkspace: MaxBoardsPerWorkspace,
        MaxMembersPerWorkspace: MaxMembersPerWorkspace,
        DefaultWorkspaceRole: DefaultWorkspaceRole,
        InvitationExpirationDays: InvitationExpirationDays,
        AllowPublicBoards: AllowPublicBoards,
        EnablePublicBoardTemplates: EnablePublicBoardTemplates,
        AllowCustomUserTemplates: AllowCustomUserTemplates,
        MaxWorkspaceNameLength: MaxWorkspaceNameLength,
        MaxBoardNameLength: MaxBoardNameLength,
        MaxBoardDescriptionLength: MaxBoardDescriptionLength,
        MaxListNameLength: MaxListNameLength,
        MaxDisplayNameLength: MaxDisplayNameLength,

        // 6. Cards, Lists & Productivity
        DefaultWipLimit: DefaultWipLimit,
        EnforceWipLimits: EnforceWipLimits,
        HighlightOverLimitLists: HighlightOverLimitLists,
        AllowCardMirroring: AllowCardMirroring,
        AllowCardSnoozing: AllowCardSnoozing,
        AllowCardVoting: AllowCardVoting,
        MaxVotesPerUserPerCard: MaxVotesPerUserPerCard,
        MaxChecklistsPerCard: MaxChecklistsPerCard,
        MaxCardTitleLength: MaxCardTitleLength,
        MaxCardDescriptionLength: MaxCardDescriptionLength,
        MaxChecklistItemLength: MaxChecklistItemLength,
        MaxAttachmentsPerCard: MaxAttachmentsPerCard,
        AutoArchiveCompletedCardsDays: AutoArchiveCompletedCardsDays,
        CardRecurrenceEnabled: CardRecurrenceEnabled,
        MaxRecurrenceIntervalDays: MaxRecurrenceIntervalDays,
        EnableTimelineView: EnableTimelineView,
        EnableCalendarView: EnableCalendarView,
        EnableTableView: EnableTableView,
        DefaultBoardView: DefaultBoardView,

        // 7. Time Tracking & Estimates
        EnableTimeTracking: EnableTimeTracking,
        EnforceTimeTrackingEstimates: EnforceTimeTrackingEstimates,
        TimeTrackingUnit: TimeTrackingUnit,

        // 8. Comments & Collaboration
        AllowCommentEditing: AllowCommentEditing,
        AllowCommentDeletion: AllowCommentDeletion,
        MaxCommentLength: MaxCommentLength,
        AllowUserMentions: AllowUserMentions,

        // 9. Labels & Custom Fields
        MaxLabelsPerBoard: MaxLabelsPerBoard,
        MaxLabelsPerCard: MaxLabelsPerCard,
        CustomFieldsEnabled: CustomFieldsEnabled,
        MaxCustomFieldsPerBoard: MaxCustomFieldsPerBoard,

        // 10. Board Automation Rules
        BoardAutomationEnabled: BoardAutomationEnabled,
        MaxAutomationRulesPerBoard: MaxAutomationRulesPerBoard,
        MaxAutomationActionsPerRule: MaxAutomationActionsPerRule,
        AutomationMonthlyRunQuotaPerUser: AutomationMonthlyRunQuotaPerUser,
        AutomationTimeoutSeconds: AutomationTimeoutSeconds,

        // 11. Notifications & System Alerts
        InAppNotificationsEnabled: InAppNotificationsEnabled,
        DueSoonThresholdHours: DueSoonThresholdHours,
        NotifyOnCardAssignment: NotifyOnCardAssignment,
        NotifyOnCardMention: NotifyOnCardMention,
        NotifyOnDueSoon: NotifyOnDueSoon,
        NotifyOnOverdue: NotifyOnOverdue,
        NotificationRetentionDays: NotificationRetentionDays,
        EnableWebPushNotifications: EnableWebPushNotifications,
        VapidSubject: VapidSubject,
        VapidPublicKey: VapidPublicKey,
        VapidPrivateKey: VapidPrivateKey,

        // 12. Dashboards & Metric Cards
        EnableDashboards: EnableDashboards,
        MaxDashcardsPerBoard: MaxDashcardsPerBoard,
        DashboardRefreshIntervalSeconds: DashboardRefreshIntervalSeconds,

        // 13. Import & Export
        EnableBoardExport: EnableBoardExport,
        EnableKanbanImport: EnableKanbanImport,
        MaxImportFileSizeMb: MaxImportFileSizeMb,

        // 14. Search & Indexing
        SearchMinQueryLength: SearchMinQueryLength,
        SearchMaxPageSize: SearchMaxPageSize,
        SearchFuzzyMatching: SearchFuzzyMatching,

        // 15. Realtime & Presence
        RealtimeBroadcastingEnabled: RealtimeBroadcastingEnabled,
        RealtimePresenceEnabled: RealtimePresenceEnabled,

        // 16. Card Aging
        CardAgingEnabled: CardAgingEnabled,
        CardAgingInactiveDays: CardAgingInactiveDays,
        CardAgingMode: CardAgingMode,

        // 17. Storage & Attachments
        MaxAttachmentSizeMb: MaxAttachmentSizeMb,
        AllowedAttachmentExtensions: AllowedAttachmentExtensions,
        AllowCoverImages: AllowCoverImages,
        MaxCoverImageSizeMb: MaxCoverImageSizeMb,
        AllowedAvatarExtensions: AllowedAvatarExtensions,
        MaxAvatarSizeMb: MaxAvatarSizeMb,
        S3BucketName: S3BucketName,
        S3EndpointUrl: S3EndpointUrl,
        S3Region: S3Region,
        S3AccessKey: S3AccessKey,
        S3SecretKey: S3SecretKey,
        S3ForcePathStyle: S3ForcePathStyle,
        BlockExecutableAttachments: BlockExecutableAttachments,
        ScanAttachmentsForMalware: ScanAttachmentsForMalware,
        ClamAvDaemonEndpoint: ClamAvDaemonEndpoint,

        // 18. Artificial Intelligence
        AiEnabled: AiEnabled,
        AiProvider: AiProvider,
        AiEndpoint: AiEndpoint,
        AiModel: AiModel,
        AiApiKey: AiApiKey,
        AiTimeoutSeconds: AiTimeoutSeconds,
        AiMaxTokens: AiMaxTokens,
        AiEnableCardDescriptionGen: AiEnableCardDescriptionGen,
        AiEnableCommentSummary: AiEnableCommentSummary,
        AiEnableAutoChecklists: AiEnableAutoChecklists,
        AiTemperature: AiTemperature,

        // 19. Outbound Email (SMTP)
        EmailNotificationsEnabled: EmailNotificationsEnabled,
        SmtpHost: SmtpHost,
        SmtpPort: SmtpPort,
        SmtpUsername: SmtpUsername,
        SmtpPassword: SmtpPassword,
        SmtpEnableSsl: SmtpEnableSsl,
        SenderEmail: SenderEmail,
        SenderName: SenderName,
        EmailRateLimitPerMinute: EmailRateLimitPerMinute,
        EmailBatchSize: EmailBatchSize,
        EmailIncludeUnsubscribeLink: EmailIncludeUnsubscribeLink,

        // 20. Inbound Email (Email-to-Board)
        InboundEmailEnabled: InboundEmailEnabled,
        InboundEmailDomain: InboundEmailDomain,
        InboundDefaultList: InboundDefaultList,
        InboundAttachSenderEmail: InboundAttachSenderEmail,

        // 21. Integrations (Slack, GitHub, Calendar, MCP, Cloud)
        SlackIntegrationEnabled: SlackIntegrationEnabled,
        SlackClientId: SlackClientId,
        SlackClientSecret: SlackClientSecret,
        SlackSigningSecret: SlackSigningSecret,
        SlackBotToken: SlackBotToken,
        GitHubIntegrationEnabled: GitHubIntegrationEnabled,
        GitHubToken: GitHubToken,
        GitHubSyncIntervalMinutes: GitHubSyncIntervalMinutes,
        GitHubAutoCloseCardsOnPrMerge: GitHubAutoCloseCardsOnPrMerge,
        GoogleCalendarIntegrationEnabled: GoogleCalendarIntegrationEnabled,
        GoogleCalendarClientId: GoogleCalendarClientId,
        GoogleCalendarClientSecret: GoogleCalendarClientSecret,
        GoogleCalendarSyncIntervalMinutes: GoogleCalendarSyncIntervalMinutes,
        McpServerEnabled: McpServerEnabled,
        CalendarIcsFeedsEnabled: CalendarIcsFeedsEnabled,
        CalendarFeedTokenLifetimeDays: CalendarFeedTokenLifetimeDays,
        GoogleDriveIntegrationEnabled: GoogleDriveIntegrationEnabled,
        OneDriveIntegrationEnabled: OneDriveIntegrationEnabled,
        DropboxIntegrationEnabled: DropboxIntegrationEnabled,
        MicrosoftTeamsIntegrationEnabled: MicrosoftTeamsIntegrationEnabled,
        MicrosoftTeamsWebhookUrl: MicrosoftTeamsWebhookUrl,
        DiscordIntegrationEnabled: DiscordIntegrationEnabled,
        DiscordWebhookUrl: DiscordWebhookUrl,
        GitLabIntegrationEnabled: GitLabIntegrationEnabled,
        GitLabEndpoint: GitLabEndpoint,

        // 22. Model Context Protocol (MCP Server)
        McpServerName: McpServerName,
        McpEnableWriteTools: McpEnableWriteTools,
        McpMaxBatchSize: McpMaxBatchSize,

        // 23. Webhooks
        WebhooksEnabled: WebhooksEnabled,
        MaxWebhookRetries: MaxWebhookRetries,
        WebhookTimeoutSeconds: WebhookTimeoutSeconds,
        WebhookPayloadSignatureEnabled: WebhookPayloadSignatureEnabled,

        // 24. API Idempotency
        EnableIdempotency: EnableIdempotency,
        IdempotencyReservationWindowMinutes: IdempotencyReservationWindowMinutes,
        IdempotencyRetentionWindowHours: IdempotencyRetentionWindowHours,

        // 25. Rate Limiting & Performance
        RateLimitingEnabled: RateLimitingEnabled,
        DefaultRequestsPerHour: DefaultRequestsPerHour,
        RateLimiterBackend: RateLimiterBackend,
        BackgroundJobPollIntervalSeconds: BackgroundJobPollIntervalSeconds,
        BackgroundJobBatchSize: BackgroundJobBatchSize,
        DefaultPageSize: DefaultPageSize,
        MaxPageSize: MaxPageSize,
        ActivityFeedPageSize: ActivityFeedPageSize,

        // 26. Infrastructure, Redis & Observability
        RedisConnectionString: RedisConnectionString,
        RedisDatabase: RedisDatabase,
        PendingTotpStoreBackend: PendingTotpStoreBackend,
        PendingTotpStoreKeyPrefix: PendingTotpStoreKeyPrefix,
        RateLimiterKeyPrefix: RateLimiterKeyPrefix,
        OtelTracingEnabled: OtelTracingEnabled,
        OtelMetricsEnabled: OtelMetricsEnabled,
        OtelEndpointUrl: OtelEndpointUrl,
        OtelServiceName: OtelServiceName,
        OtelTraceSampleRate: OtelTraceSampleRate,
        OutboxProcessorEnabled: OutboxProcessorEnabled,
        OutboxBatchSize: OutboxBatchSize,
        OutboxPollIntervalSeconds: OutboxPollIntervalSeconds,
        OutboxMaxAttempts: OutboxMaxAttempts,
        OutboxMessageRetentionDays: OutboxMessageRetentionDays,
        ResponseCompressionEnabled: ResponseCompressionEnabled,
        ResponseCompressionLevel: ResponseCompressionLevel,
        StaticFilesMaxAgeSeconds: StaticFilesMaxAgeSeconds,
        ForwardedHeadersEnabled: ForwardedHeadersEnabled,
        MaxRequestBodySizeMb: MaxRequestBodySizeMb,
        DatabaseCommandTimeoutSeconds: DatabaseCommandTimeoutSeconds,
        DatabaseMaxRetryCount: DatabaseMaxRetryCount,
        DatabaseMaxRetryDelaySeconds: DatabaseMaxRetryDelaySeconds,
        DatabaseEnableDetailedErrors: DatabaseEnableDetailedErrors,
        DatabaseEnableSensitiveDataLogging: DatabaseEnableSensitiveDataLogging,
        RunMigrationsOnStartup: RunMigrationsOnStartup,
        EnableSwaggerInProduction: EnableSwaggerInProduction,

        // 27. Activities & Audit Logging
        EnableActivityLogging: EnableActivityLogging,
        ActivityPageSize: ActivityPageSize,
        AuditLogDetailedIp: AuditLogDetailedIp,

        // 28. System Logging (Serilog)
        LogRetainedFileCountLimit: LogRetainedFileCountLimit,
        LogFileSizeLimitMb: LogFileSizeLimitMb,

        // 29. Data Retention, Residency & GDPR
        RetentionSweeperEnabled: RetentionSweeperEnabled,
        SweepIntervalHours: SweepIntervalHours,
        UserGracePeriodDays: UserGracePeriodDays,
        ActivityRetentionDays: ActivityRetentionDays,
        AuditRetentionDays: AuditRetentionDays,
        AutoPurgeOrphanAttachments: AutoPurgeOrphanAttachments,
        DataResidencyEnabled: DataResidencyEnabled,
        DeploymentRegion: DeploymentRegion,
        EnforceDataResidency: EnforceDataResidency,
        SoftDeleteRetentionDays: SoftDeleteRetentionDays,
        PermanentDeleteRequiresAdmin: PermanentDeleteRequiresAdmin,
        CustomPrivacyPolicyUrl: CustomPrivacyPolicyUrl ?? string.Empty,
        CustomTermsOfServiceUrl: CustomTermsOfServiceUrl ?? string.Empty,
        DisplayCookieBanner: DisplayCookieBanner,
        RequireLegalNoticeAcceptance: RequireLegalNoticeAcceptance,

        // 30. Automated Backups
        AutoBackupEnabled: AutoBackupEnabled,
        BackupIntervalHours: BackupIntervalHours,
        BackupRetentionDays: BackupRetentionDays,

        // 31. Seeder & Dev
        SeederEnabled: SeederEnabled,
        AllowSeederExecution: AllowSeederExecution,
        SeederWipeBeforeSeed: SeederWipeBeforeSeed
    );
}
