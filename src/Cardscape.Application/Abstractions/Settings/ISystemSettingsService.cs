namespace Cardscape.Application.Abstractions.Settings;

public sealed record SystemSettingsDto(
    // 1. General, Brand & Whitelabel
    string InstanceTitle = "Cardscape",
    string SupportEmail = "support@cardscape.local",
    string DefaultLanguage = "es",
    string DefaultTheme = "default",
    bool AllowPublicRegistration = true,
    string WelcomeMessage = "Bienvenido a Cardscape",
    bool EnableKeyboardShortcuts = true,
    bool EnableDenseModeByDefault = false,
    bool ShowCardCoverImagesByDefault = true,
    bool EnableSoundNotifications = true,
    string CustomLogoUrl = "",
    string CustomFaviconUrl = "",
    bool HidePoweredByCardscape = false,
    bool MaintenanceModeEnabled = false,
    string MaintenanceModeMessage = "El sistema se encuentra en mantenimiento programado.",
    bool SystemAnnouncementEnabled = false,
    string SystemAnnouncementMessage = "",
    string SystemAnnouncementType = "Info",

    // 2. Security & Policy
    int JwtAccessTokenMinutes = 1440,
    int PasswordMinLength = 8,
    bool PasswordRequireDigit = true,
    bool PasswordRequireNonAlphanumeric = false,
    bool RequireTwoFactorForAdmins = false,
    int MaxFailedLoginAttempts = 5,
    int LockoutDurationMinutes = 15,
    bool SingleActiveSessionPerUser = false,
    int MaxApiTokensPerUser = 10,
    int ApiTokenExpirationDays = 90,
    int DefaultApiTokenExpiryDays = 90,
    int MaxApiTokenExpiryDays = 365,
    bool CacheAdminClaim = true,
    string TotpIssuerName = "Cardscape",
    int TotpStepTolerance = 1,
    int PasswordResetTokenLifetimeMinutes = 60,
    int IdleSessionTimeoutMinutes = 0,
    int MaxConcurrentSessionsPerUser = 5,
    int PasswordExpiryDays = 0,
    int PasswordHistoryCount = 0,
    string CorsAllowedOrigins = "*",
    bool EnforceHttps = true,
    bool EnableSecurityHeaders = true,
    int HstsMaxAgeSeconds = 31536000,
    bool HstsIncludeSubdomains = true,
    bool HstsPreload = false,
    string ContentSecurityPolicy = "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self' wss: https:;",
    string XFrameOptions = "DENY",
    string ReferrerPolicy = "no-referrer",
    int DataProtectionKeyLifetimeDays = 90,
    string DataProtectionKeyDirectory = "",

    // 3. Enterprise SSO & Provisioning
    bool SamlSsoEnabled = false,
    string SamlEnforceForDomain = "",
    bool ScimProvisioningEnabled = false,
    int ScimTokenExpirationDays = 180,
    bool OAuthAppsEnabled = true,
    int MaxOAuthAppsPerUser = 5,

    // 4. External / Social OAuth
    bool EnableGoogleAuth = false,
    string GoogleClientId = "",
    string GoogleClientSecretMasked = "",
    bool EnableGitHubAuth = false,
    string GitHubClientId = "",
    string GitHubClientSecretMasked = "",
    bool EnableMicrosoftAuth = false,
    string MicrosoftClientId = "",
    string MicrosoftClientSecretMasked = "",
    bool EnableAppleAuth = false,
    string AppleClientId = "",
    string AppleTeamId = "",
    string AppleKeyId = "",
    string ApplePrivateKeyPemMasked = "",

    // 5. Workspaces & Boards
    int MaxWorkspacesPerUser = 0,
    int MaxBoardsPerWorkspace = 0,
    int MaxMembersPerWorkspace = 0,
    string DefaultWorkspaceRole = "Member",
    int InvitationExpirationDays = 7,
    bool AllowPublicBoards = true,
    bool EnablePublicBoardTemplates = true,
    bool AllowCustomUserTemplates = true,

    // 6. Cards, Lists & Productivity
    int DefaultWipLimit = 0,
    bool EnforceWipLimits = false,
    bool HighlightOverLimitLists = true,
    bool AllowCardMirroring = true,
    bool AllowCardSnoozing = true,
    bool AllowCardVoting = true,
    int MaxVotesPerUserPerCard = 1,
    int MaxChecklistsPerCard = 10,
    int AutoArchiveCompletedCardsDays = 0,
    bool CardRecurrenceEnabled = true,
    int MaxRecurrenceIntervalDays = 365,
    bool EnableTimelineView = true,
    bool EnableCalendarView = true,
    bool EnableTableView = true,
    string DefaultBoardView = "Kanban",

    // 7. Time Tracking & Estimates
    bool EnableTimeTracking = false,
    bool EnforceTimeTrackingEstimates = false,
    string TimeTrackingUnit = "Hours",

    // 8. Comments & Collaboration
    bool AllowCommentEditing = true,
    bool AllowCommentDeletion = true,
    int MaxCommentLength = 5000,
    bool AllowUserMentions = true,

    // 9. Labels & Custom Fields
    int MaxLabelsPerBoard = 50,
    int MaxLabelsPerCard = 10,
    bool CustomFieldsEnabled = true,
    int MaxCustomFieldsPerBoard = 30,

    // 10. Board Automation Rules
    bool BoardAutomationEnabled = true,
    int MaxAutomationRulesPerBoard = 20,
    int MaxAutomationActionsPerRule = 5,
    int AutomationMonthlyRunQuotaPerUser = 250,
    int AutomationTimeoutSeconds = 15,

    // 11. Notifications & System Alerts
    bool InAppNotificationsEnabled = true,
    int DueSoonThresholdHours = 24,
    bool NotifyOnCardAssignment = true,
    bool NotifyOnCardMention = true,
    bool NotifyOnDueSoon = true,
    bool NotifyOnOverdue = true,
    int NotificationRetentionDays = 30,
    bool EnableWebPushNotifications = false,
    string VapidSubject = "mailto:admin@cardscape.local",
    string VapidPublicKey = "",
    string VapidPrivateKeyMasked = "",

    // 12. Dashboards & Metric Cards
    bool EnableDashboards = true,
    int MaxDashcardsPerBoard = 10,
    int DashboardRefreshIntervalSeconds = 60,

    // 13. Import & Export
    bool EnableBoardExport = true,
    bool EnableKanbanImport = true,
    int MaxImportFileSizeMb = 50,

    // 14. Search & Indexing
    int SearchMinQueryLength = 2,
    int SearchMaxPageSize = 50,
    bool SearchFuzzyMatching = true,

    // 15. Realtime & Presence
    bool RealtimeBroadcastingEnabled = true,
    bool RealtimePresenceEnabled = true,

    // 16. Card Aging
    bool CardAgingEnabled = true,
    int CardAgingInactiveDays = 14,
    string CardAgingMode = "Regular",

    // 17. Storage & Attachments
    string StorageProvider = "LocalFile",
    string StorageRoot = "Storage",
    int MaxAttachmentSizeMb = 25,
    string AllowedAttachmentExtensions = "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip",
    bool AllowCoverImages = true,
    int MaxCoverImageSizeMb = 5,
    string S3BucketName = "",
    string S3EndpointUrl = "",
    string S3Region = "us-east-1",
    string S3AccessKey = "",
    string S3SecretKeyMasked = "",
    bool S3ForcePathStyle = true,
    bool BlockExecutableAttachments = true,
    bool ScanAttachmentsForMalware = false,
    string ClamAvDaemonEndpoint = "",

    // 18. Artificial Intelligence
    bool AiEnabled = false,
    string AiProvider = "OpenAiCompatible",
    string AiEndpoint = "http://localhost:11434/",
    string AiModel = "llama3.2",
    string AiApiKeyMasked = "",
    int AiTimeoutSeconds = 60,
    int AiMaxTokens = 2048,
    bool AiEnableCardDescriptionGen = true,
    bool AiEnableCommentSummary = true,
    bool AiEnableAutoChecklists = true,
    int AiTemperature = 70,

    // 19. Outbound Email (SMTP)
    bool EmailNotificationsEnabled = false,
    string SmtpHost = "localhost",
    int SmtpPort = 587,
    string SmtpUsername = "",
    string SmtpPasswordMasked = "",
    bool SmtpEnableSsl = true,
    string SenderEmail = "noreply@cardscape.local",
    string SenderName = "Cardscape Notificaciones",

    // 20. Inbound Email (Email-to-Board)
    bool InboundEmailEnabled = false,
    string InboundEmailDomain = "inbound.cardscape.local",
    string InboundDefaultList = "Inbox",
    bool InboundAttachSenderEmail = true,

    // 21. Integrations (Slack, GitHub, Calendar, MCP, Cloud)
    bool SlackIntegrationEnabled = false,
    string SlackClientId = "",
    string SlackClientSecretMasked = "",
    string SlackSigningSecretMasked = "",
    string SlackBotTokenMasked = "",
    bool GitHubIntegrationEnabled = false,
    string GitHubTokenMasked = "",
    int GitHubSyncIntervalMinutes = 15,
    bool GitHubAutoCloseCardsOnPrMerge = true,
    bool GoogleCalendarIntegrationEnabled = false,
    string GoogleCalendarClientId = "",
    string GoogleCalendarClientSecretMasked = "",
    int GoogleCalendarSyncIntervalMinutes = 15,
    bool McpServerEnabled = true,
    bool CalendarIcsFeedsEnabled = true,
    int CalendarFeedTokenLifetimeDays = 180,
    bool GoogleDriveIntegrationEnabled = false,
    bool OneDriveIntegrationEnabled = false,
    bool DropboxIntegrationEnabled = false,
    bool MicrosoftTeamsIntegrationEnabled = false,
    string MicrosoftTeamsWebhookUrlMasked = "",
    bool DiscordIntegrationEnabled = false,
    string DiscordWebhookUrlMasked = "",
    bool GitLabIntegrationEnabled = false,
    string GitLabEndpoint = "",

    // 22. Model Context Protocol (MCP Server)
    string McpServerName = "Cardscape-MCP",
    bool McpEnableWriteTools = true,
    int McpMaxBatchSize = 50,

    // 23. Webhooks
    bool WebhooksEnabled = true,
    int MaxWebhookRetries = 3,
    int WebhookTimeoutSeconds = 10,
    bool WebhookPayloadSignatureEnabled = true,

    // 24. API Idempotency
    bool EnableIdempotency = true,
    int IdempotencyReservationWindowMinutes = 15,
    int IdempotencyRetentionWindowHours = 24,

    // 25. Rate Limiting & Performance
    bool RateLimitingEnabled = true,
    int DefaultRequestsPerHour = 1000,
    string RateLimiterBackend = "InMemory",
    int BackgroundJobPollIntervalSeconds = 2,
    int BackgroundJobBatchSize = 10,

    // 26. Infrastructure, Redis & Observability
    string RedisConnectionStringMasked = "",
    int RedisDatabase = 0,
    string PendingTotpStoreBackend = "InMemory",
    string PendingTotpStoreKeyPrefix = "cardscape:totp-pending:",
    string RateLimiterKeyPrefix = "cardscape:rl:",
    bool OtelTracingEnabled = false,
    bool OtelMetricsEnabled = false,
    string OtelEndpointUrl = "",
    string OtelServiceName = "Cardscape.Api",
    int OtelTraceSampleRate = 100,
    bool OutboxProcessorEnabled = true,
    int OutboxBatchSize = 50,
    int OutboxPollIntervalSeconds = 5,
    int OutboxMaxAttempts = 5,
    int OutboxMessageRetentionDays = 14,

    // 27. Activities & Audit Logging
    bool EnableActivityLogging = true,
    int ActivityPageSize = 25,
    bool AuditLogDetailedIp = true,

    // 28. System Logging (Serilog)
    int LogRetainedFileCountLimit = 30,
    int LogFileSizeLimitMb = 100,

    // 29. Data Retention, Residency & GDPR
    bool RetentionSweeperEnabled = true,
    int SweepIntervalHours = 6,
    int UserGracePeriodDays = 30,
    int ActivityRetentionDays = 365,
    int AuditRetentionDays = 730,
    bool AutoPurgeOrphanAttachments = true,
    bool DataResidencyEnabled = false,
    string DeploymentRegion = "Unspecified",
    bool EnforceDataResidency = false,
    int SoftDeleteRetentionDays = 30,
    bool PermanentDeleteRequiresAdmin = true,

    // 30. Legal Notices, Privacy & Compliance
    string CustomPrivacyPolicyUrl = "",
    string CustomTermsOfServiceUrl = "",
    bool DisplayCookieBanner = false,
    bool RequireLegalNoticeAcceptance = false,

    // 31. Automated Backups
    bool AutoBackupEnabled = false,
    int BackupIntervalHours = 24,
    int BackupRetentionDays = 30,

    // 32. Seeder & Dev
    bool SeederEnabled = false,
    bool AllowSeederExecution = false,
    bool SeederWipeBeforeSeed = false,

    // 33. Live Diagnostics (Read-Only)
    string DatabaseProvider = "Sqlite",
    string DatabaseHealth = "Healthy",
    string Environment = "Development",
    string AppVersion = "1.2.0",
    string Uptime = "0m",
    long MemoryUsageMb = 0,
    long FreeDiskSpaceMb = 0,
    int ActiveThreads = 0);

public sealed record UpdateSystemSettingsRequest(
    // 1. General, Brand & Whitelabel
    string InstanceTitle = "Cardscape",
    string SupportEmail = "support@cardscape.local",
    string DefaultLanguage = "es",
    string DefaultTheme = "default",
    bool AllowPublicRegistration = true,
    string WelcomeMessage = "Bienvenido a Cardscape",
    bool EnableKeyboardShortcuts = true,
    bool EnableDenseModeByDefault = false,
    bool ShowCardCoverImagesByDefault = true,
    bool EnableSoundNotifications = true,
    string CustomLogoUrl = "",
    string CustomFaviconUrl = "",
    bool HidePoweredByCardscape = false,
    bool MaintenanceModeEnabled = false,
    string MaintenanceModeMessage = "El sistema se encuentra en mantenimiento programado.",
    bool SystemAnnouncementEnabled = false,
    string SystemAnnouncementMessage = "",
    string SystemAnnouncementType = "Info",

    // 2. Security & Policy
    int JwtAccessTokenMinutes = 1440,
    int PasswordMinLength = 8,
    bool PasswordRequireDigit = true,
    bool PasswordRequireNonAlphanumeric = false,
    bool RequireTwoFactorForAdmins = false,
    int MaxFailedLoginAttempts = 5,
    int LockoutDurationMinutes = 15,
    bool SingleActiveSessionPerUser = false,
    int MaxApiTokensPerUser = 10,
    int ApiTokenExpirationDays = 90,
    int DefaultApiTokenExpiryDays = 90,
    int MaxApiTokenExpiryDays = 365,
    bool CacheAdminClaim = true,
    string TotpIssuerName = "Cardscape",
    int TotpStepTolerance = 1,
    int PasswordResetTokenLifetimeMinutes = 60,
    int IdleSessionTimeoutMinutes = 0,
    int MaxConcurrentSessionsPerUser = 5,
    int PasswordExpiryDays = 0,
    int PasswordHistoryCount = 0,
    string CorsAllowedOrigins = "*",
    bool EnforceHttps = true,
    bool EnableSecurityHeaders = true,
    int HstsMaxAgeSeconds = 31536000,
    bool HstsIncludeSubdomains = true,
    bool HstsPreload = false,
    string ContentSecurityPolicy = "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self' wss: https:;",
    string XFrameOptions = "DENY",
    string ReferrerPolicy = "no-referrer",
    int DataProtectionKeyLifetimeDays = 90,
    string DataProtectionKeyDirectory = "",

    // 3. Enterprise SSO & Provisioning
    bool SamlSsoEnabled = false,
    string SamlEnforceForDomain = "",
    bool ScimProvisioningEnabled = false,
    int ScimTokenExpirationDays = 180,
    bool OAuthAppsEnabled = true,
    int MaxOAuthAppsPerUser = 5,

    // 4. External / Social OAuth
    bool EnableGoogleAuth = false,
    string GoogleClientId = "",
    string? GoogleClientSecret = null,
    bool EnableGitHubAuth = false,
    string GitHubClientId = "",
    string? GitHubClientSecret = null,
    bool EnableMicrosoftAuth = false,
    string MicrosoftClientId = "",
    string? MicrosoftClientSecret = null,
    bool EnableAppleAuth = false,
    string AppleClientId = "",
    string AppleTeamId = "",
    string AppleKeyId = "",
    string? ApplePrivateKeyPem = null,

    // 5. Workspaces & Boards
    int MaxWorkspacesPerUser = 0,
    int MaxBoardsPerWorkspace = 0,
    int MaxMembersPerWorkspace = 0,
    string DefaultWorkspaceRole = "Member",
    int InvitationExpirationDays = 7,
    bool AllowPublicBoards = true,
    bool EnablePublicBoardTemplates = true,
    bool AllowCustomUserTemplates = true,

    // 6. Cards, Lists & Productivity
    int DefaultWipLimit = 0,
    bool EnforceWipLimits = false,
    bool HighlightOverLimitLists = true,
    bool AllowCardMirroring = true,
    bool AllowCardSnoozing = true,
    bool AllowCardVoting = true,
    int MaxVotesPerUserPerCard = 1,
    int MaxChecklistsPerCard = 10,
    int AutoArchiveCompletedCardsDays = 0,
    bool CardRecurrenceEnabled = true,
    int MaxRecurrenceIntervalDays = 365,
    bool EnableTimelineView = true,
    bool EnableCalendarView = true,
    bool EnableTableView = true,
    string DefaultBoardView = "Kanban",

    // 7. Time Tracking & Estimates
    bool EnableTimeTracking = false,
    bool EnforceTimeTrackingEstimates = false,
    string TimeTrackingUnit = "Hours",

    // 8. Comments & Collaboration
    bool AllowCommentEditing = true,
    bool AllowCommentDeletion = true,
    int MaxCommentLength = 5000,
    bool AllowUserMentions = true,

    // 9. Labels & Custom Fields
    int MaxLabelsPerBoard = 50,
    int MaxLabelsPerCard = 10,
    bool CustomFieldsEnabled = true,
    int MaxCustomFieldsPerBoard = 30,

    // 10. Board Automation Rules
    bool BoardAutomationEnabled = true,
    int MaxAutomationRulesPerBoard = 20,
    int MaxAutomationActionsPerRule = 5,
    int AutomationMonthlyRunQuotaPerUser = 250,
    int AutomationTimeoutSeconds = 15,

    // 11. Notifications & System Alerts
    bool InAppNotificationsEnabled = true,
    int DueSoonThresholdHours = 24,
    bool NotifyOnCardAssignment = true,
    bool NotifyOnCardMention = true,
    bool NotifyOnDueSoon = true,
    bool NotifyOnOverdue = true,
    int NotificationRetentionDays = 30,
    bool EnableWebPushNotifications = false,
    string VapidSubject = "mailto:admin@cardscape.local",
    string VapidPublicKey = "",
    string? VapidPrivateKey = null,

    // 12. Dashboards & Metric Cards
    bool EnableDashboards = true,
    int MaxDashcardsPerBoard = 10,
    int DashboardRefreshIntervalSeconds = 60,

    // 13. Import & Export
    bool EnableBoardExport = true,
    bool EnableKanbanImport = true,
    int MaxImportFileSizeMb = 50,

    // 14. Search & Indexing
    int SearchMinQueryLength = 2,
    int SearchMaxPageSize = 50,
    bool SearchFuzzyMatching = true,

    // 15. Realtime & Presence
    bool RealtimeBroadcastingEnabled = true,
    bool RealtimePresenceEnabled = true,

    // 16. Card Aging
    bool CardAgingEnabled = true,
    int CardAgingInactiveDays = 14,
    string CardAgingMode = "Regular",

    // 17. Storage & Attachments
    int MaxAttachmentSizeMb = 25,
    string AllowedAttachmentExtensions = "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip",
    bool AllowCoverImages = true,
    int MaxCoverImageSizeMb = 5,
    string S3BucketName = "",
    string S3EndpointUrl = "",
    string S3Region = "us-east-1",
    string S3AccessKey = "",
    string? S3SecretKey = null,
    bool S3ForcePathStyle = true,
    bool BlockExecutableAttachments = true,
    bool ScanAttachmentsForMalware = false,
    string ClamAvDaemonEndpoint = "",

    // 18. Artificial Intelligence
    bool AiEnabled = false,
    string AiProvider = "OpenAiCompatible",
    string AiEndpoint = "http://localhost:11434/",
    string AiModel = "llama3.2",
    string? AiApiKey = null,
    int AiTimeoutSeconds = 60,
    int AiMaxTokens = 2048,
    bool AiEnableCardDescriptionGen = true,
    bool AiEnableCommentSummary = true,
    bool AiEnableAutoChecklists = true,
    int AiTemperature = 70,

    // 19. Outbound Email (SMTP)
    bool EmailNotificationsEnabled = false,
    string SmtpHost = "localhost",
    int SmtpPort = 587,
    string SmtpUsername = "",
    string? SmtpPassword = null,
    bool SmtpEnableSsl = true,
    string SenderEmail = "noreply@cardscape.local",
    string SenderName = "Cardscape Notificaciones",

    // 20. Inbound Email (Email-to-Board)
    bool InboundEmailEnabled = false,
    string InboundEmailDomain = "inbound.cardscape.local",
    string InboundDefaultList = "Inbox",
    bool InboundAttachSenderEmail = true,

    // 21. Integrations (Slack, GitHub, Calendar, MCP, Cloud)
    bool SlackIntegrationEnabled = false,
    string SlackClientId = "",
    string? SlackClientSecret = null,
    string? SlackSigningSecret = null,
    string? SlackBotToken = null,
    bool GitHubIntegrationEnabled = false,
    string? GitHubToken = null,
    int GitHubSyncIntervalMinutes = 15,
    bool GitHubAutoCloseCardsOnPrMerge = true,
    bool GoogleCalendarIntegrationEnabled = false,
    string GoogleCalendarClientId = "",
    string? GoogleCalendarClientSecret = null,
    int GoogleCalendarSyncIntervalMinutes = 15,
    bool McpServerEnabled = true,
    bool CalendarIcsFeedsEnabled = true,
    int CalendarFeedTokenLifetimeDays = 180,
    bool GoogleDriveIntegrationEnabled = false,
    bool OneDriveIntegrationEnabled = false,
    bool DropboxIntegrationEnabled = false,
    bool MicrosoftTeamsIntegrationEnabled = false,
    string? MicrosoftTeamsWebhookUrl = null,
    bool DiscordIntegrationEnabled = false,
    string? DiscordWebhookUrl = null,
    bool GitLabIntegrationEnabled = false,
    string GitLabEndpoint = "",

    // 22. Model Context Protocol (MCP Server)
    string McpServerName = "Cardscape-MCP",
    bool McpEnableWriteTools = true,
    int McpMaxBatchSize = 50,

    // 23. Webhooks
    bool WebhooksEnabled = true,
    int MaxWebhookRetries = 3,
    int WebhookTimeoutSeconds = 10,
    bool WebhookPayloadSignatureEnabled = true,

    // 24. API Idempotency
    bool EnableIdempotency = true,
    int IdempotencyReservationWindowMinutes = 15,
    int IdempotencyRetentionWindowHours = 24,

    // 25. Rate Limiting & Performance
    bool RateLimitingEnabled = true,
    int DefaultRequestsPerHour = 1000,
    string RateLimiterBackend = "InMemory",
    int BackgroundJobPollIntervalSeconds = 2,
    int BackgroundJobBatchSize = 10,

    // 26. Infrastructure, Redis & Observability
    string? RedisConnectionString = null,
    int RedisDatabase = 0,
    string PendingTotpStoreBackend = "InMemory",
    string PendingTotpStoreKeyPrefix = "cardscape:totp-pending:",
    string RateLimiterKeyPrefix = "cardscape:rl:",
    bool OtelTracingEnabled = false,
    bool OtelMetricsEnabled = false,
    string OtelEndpointUrl = "",
    string OtelServiceName = "Cardscape.Api",
    int OtelTraceSampleRate = 100,
    bool OutboxProcessorEnabled = true,
    int OutboxBatchSize = 50,
    int OutboxPollIntervalSeconds = 5,
    int OutboxMaxAttempts = 5,
    int OutboxMessageRetentionDays = 14,

    // 27. Activities & Audit Logging
    bool EnableActivityLogging = true,
    int ActivityPageSize = 25,
    bool AuditLogDetailedIp = true,

    // 28. System Logging (Serilog)
    int LogRetainedFileCountLimit = 30,
    int LogFileSizeLimitMb = 100,

    // 29. Data Retention, Residency & GDPR
    bool RetentionSweeperEnabled = true,
    int SweepIntervalHours = 6,
    int UserGracePeriodDays = 30,
    int ActivityRetentionDays = 365,
    int AuditRetentionDays = 730,
    bool AutoPurgeOrphanAttachments = true,
    bool DataResidencyEnabled = false,
    string DeploymentRegion = "Unspecified",
    bool EnforceDataResidency = false,
    int SoftDeleteRetentionDays = 30,
    bool PermanentDeleteRequiresAdmin = true,

    // 30. Legal Notices, Privacy & Compliance
    string CustomPrivacyPolicyUrl = "",
    string CustomTermsOfServiceUrl = "",
    bool DisplayCookieBanner = false,
    bool RequireLegalNoticeAcceptance = false,

    // 31. Automated Backups
    bool AutoBackupEnabled = false,
    int BackupIntervalHours = 24,
    int BackupRetentionDays = 30,

    // 32. Seeder & Dev
    bool SeederEnabled = false,
    bool AllowSeederExecution = false,
    bool SeederWipeBeforeSeed = false);

public sealed record TestEmailRequest(string TargetEmail);

public sealed record TestAiResponse(bool Success, string Message, string? ModelUsed = null);

public sealed record TestEmailResponse(bool Success, string Message);

public interface ISystemSettingsService
{
    Task<SystemSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<SystemSettingsDto> UpdateSettingsAsync(UpdateSystemSettingsRequest request, string? updatedBy, CancellationToken ct = default);
    Task<SystemSettingsDto> ResetToDefaultsAsync(string? resetBy, CancellationToken ct = default);
    Task<bool> IsPublicRegistrationAllowedAsync(CancellationToken ct = default);
    Task<TestAiResponse> TestAiConnectionAsync(CancellationToken ct = default);
    Task<TestEmailResponse> TestEmailAsync(string targetEmail, CancellationToken ct = default);
}
