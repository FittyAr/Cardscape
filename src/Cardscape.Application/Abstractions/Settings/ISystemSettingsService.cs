namespace Cardscape.Application.Abstractions.Settings;

public sealed record SystemSettingsDto(
    // 1. General & Brand
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
    string CorsAllowedOrigins = "*",
    bool EnforceHttps = true,
    bool EnableSecurityHeaders = true,

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

    // 5. Workspaces & Boards
    int MaxWorkspacesPerUser = 0,
    int MaxBoardsPerWorkspace = 0,
    int MaxMembersPerWorkspace = 0,
    string DefaultWorkspaceRole = "Member",
    int InvitationExpirationDays = 7,
    bool AllowPublicBoards = true,

    // 6. Cards, Lists & Productivity
    int DefaultWipLimit = 0,
    bool EnforceWipLimits = false,
    bool AllowCardMirroring = true,
    bool AllowCardSnoozing = true,
    bool AllowCardVoting = true,
    int MaxVotesPerUserPerCard = 1,
    int MaxChecklistsPerCard = 10,
    int AutoArchiveCompletedCardsDays = 0,
    bool CardRecurrenceEnabled = true,
    int MaxRecurrenceIntervalDays = 365,

    // 7. Comments & Collaboration
    bool AllowCommentEditing = true,
    bool AllowCommentDeletion = true,
    int MaxCommentLength = 5000,
    bool AllowUserMentions = true,

    // 8. Labels & Custom Fields
    int MaxLabelsPerBoard = 50,
    int MaxLabelsPerCard = 10,
    bool CustomFieldsEnabled = true,
    int MaxCustomFieldsPerBoard = 30,

    // 9. Board Automation Rules
    bool BoardAutomationEnabled = true,
    int MaxAutomationRulesPerBoard = 20,
    int MaxAutomationActionsPerRule = 5,

    // 10. Notifications & System Alerts
    bool InAppNotificationsEnabled = true,
    int DueSoonThresholdHours = 24,
    bool NotifyOnCardAssignment = true,
    bool NotifyOnCardMention = true,
    bool NotifyOnDueSoon = true,
    bool NotifyOnOverdue = true,
    int NotificationRetentionDays = 30,

    // 11. Dashboards & Metric Cards
    bool EnableDashboards = true,
    int MaxDashcardsPerBoard = 10,
    int DashboardRefreshIntervalSeconds = 60,

    // 12. Import & Export
    bool EnableBoardExport = true,
    bool EnableKanbanImport = true,
    int MaxImportFileSizeMb = 50,

    // 13. Search & Indexing
    int SearchMinQueryLength = 2,
    int SearchMaxPageSize = 50,
    bool SearchFuzzyMatching = true,

    // 14. Realtime & Presence
    bool RealtimeBroadcastingEnabled = true,
    bool RealtimePresenceEnabled = true,

    // 15. Card Aging
    bool CardAgingEnabled = true,
    int CardAgingInactiveDays = 14,
    string CardAgingMode = "Regular",

    // 16. Storage & Attachments
    string StorageProvider = "LocalFile",
    string StorageRoot = "Storage",
    int MaxAttachmentSizeMb = 25,
    string AllowedAttachmentExtensions = "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip",
    bool AllowCoverImages = true,
    int MaxCoverImageSizeMb = 5,

    // 17. Artificial Intelligence
    bool AiEnabled = false,
    string AiProvider = "OpenAiCompatible",
    string AiEndpoint = "http://localhost:11434/",
    string AiModel = "llama3.2",
    string AiApiKeyMasked = "",
    int AiTimeoutSeconds = 60,
    int AiMaxTokens = 2048,

    // 18. Outbound Email (SMTP)
    bool EmailNotificationsEnabled = false,
    string SmtpHost = "localhost",
    int SmtpPort = 587,
    string SmtpUsername = "",
    string SmtpPasswordMasked = "",
    bool SmtpEnableSsl = true,
    string SenderEmail = "noreply@cardscape.local",
    string SenderName = "Cardscape Notificaciones",

    // 19. Inbound Email (Email-to-Board)
    bool InboundEmailEnabled = false,
    string InboundEmailDomain = "inbound.cardscape.local",
    string InboundDefaultList = "Inbox",
    bool InboundAttachSenderEmail = true,

    // 20. Integrations (Slack, GitHub, Calendar, MCP)
    bool SlackIntegrationEnabled = false,
    bool GitHubIntegrationEnabled = false,
    bool GoogleCalendarIntegrationEnabled = false,
    bool McpServerEnabled = true,
    bool CalendarIcsFeedsEnabled = true,
    int CalendarFeedTokenLifetimeDays = 180,

    // 21. Model Context Protocol (MCP Server)
    string McpServerName = "Cardscape-MCP",
    bool McpEnableWriteTools = true,
    int McpMaxBatchSize = 50,

    // 22. Webhooks
    bool WebhooksEnabled = true,
    int MaxWebhookRetries = 3,
    int WebhookTimeoutSeconds = 10,
    bool WebhookPayloadSignatureEnabled = true,

    // 23. API Idempotency
    bool EnableIdempotency = true,
    int IdempotencyReservationWindowMinutes = 15,
    int IdempotencyRetentionWindowHours = 24,

    // 24. Rate Limiting & Performance
    bool RateLimitingEnabled = true,
    int DefaultRequestsPerHour = 1000,
    string RateLimiterBackend = "InMemory",
    int BackgroundJobPollIntervalSeconds = 2,
    int BackgroundJobBatchSize = 10,

    // 25. Infrastructure & Redis
    string RedisConnectionStringMasked = "",
    int RedisDatabase = 0,
    string PendingTotpStoreBackend = "InMemory",
    string PendingTotpStoreKeyPrefix = "cardscape:totp-pending:",
    string RateLimiterKeyPrefix = "cardscape:rl:",

    // 26. Activities & Audit Logging
    bool EnableActivityLogging = true,
    int ActivityPageSize = 25,
    bool AuditLogDetailedIp = true,

    // 27. System Logging (Serilog)
    int LogRetainedFileCountLimit = 30,
    int LogFileSizeLimitMb = 100,

    // 28. Data Retention, Residency & GDPR
    bool RetentionSweeperEnabled = true,
    int SweepIntervalHours = 6,
    int UserGracePeriodDays = 30,
    int ActivityRetentionDays = 365,
    int AuditRetentionDays = 730,
    bool AutoPurgeOrphanAttachments = true,
    bool DataResidencyEnabled = false,
    string DeploymentRegion = "Unspecified",
    bool EnforceDataResidency = false,

    // 29. Seeder & Dev
    bool SeederEnabled = false,
    bool AllowSeederExecution = false,
    bool SeederWipeBeforeSeed = false,

    // 30. Live Diagnostics (Read-Only)
    string DatabaseProvider = "Sqlite",
    string DatabaseHealth = "Healthy",
    string Environment = "Development",
    string AppVersion = "1.2.0",
    string Uptime = "0m",
    long MemoryUsageMb = 0,
    long FreeDiskSpaceMb = 0,
    int ActiveThreads = 0);

public sealed record UpdateSystemSettingsRequest(
    // 1. General & Brand
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
    string CorsAllowedOrigins = "*",
    bool EnforceHttps = true,
    bool EnableSecurityHeaders = true,

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

    // 5. Workspaces & Boards
    int MaxWorkspacesPerUser = 0,
    int MaxBoardsPerWorkspace = 0,
    int MaxMembersPerWorkspace = 0,
    string DefaultWorkspaceRole = "Member",
    int InvitationExpirationDays = 7,
    bool AllowPublicBoards = true,

    // 6. Cards, Lists & Productivity
    int DefaultWipLimit = 0,
    bool EnforceWipLimits = false,
    bool AllowCardMirroring = true,
    bool AllowCardSnoozing = true,
    bool AllowCardVoting = true,
    int MaxVotesPerUserPerCard = 1,
    int MaxChecklistsPerCard = 10,
    int AutoArchiveCompletedCardsDays = 0,
    bool CardRecurrenceEnabled = true,
    int MaxRecurrenceIntervalDays = 365,

    // 7. Comments & Collaboration
    bool AllowCommentEditing = true,
    bool AllowCommentDeletion = true,
    int MaxCommentLength = 5000,
    bool AllowUserMentions = true,

    // 8. Labels & Custom Fields
    int MaxLabelsPerBoard = 50,
    int MaxLabelsPerCard = 10,
    bool CustomFieldsEnabled = true,
    int MaxCustomFieldsPerBoard = 30,

    // 9. Board Automation Rules
    bool BoardAutomationEnabled = true,
    int MaxAutomationRulesPerBoard = 20,
    int MaxAutomationActionsPerRule = 5,

    // 10. Notifications & System Alerts
    bool InAppNotificationsEnabled = true,
    int DueSoonThresholdHours = 24,
    bool NotifyOnCardAssignment = true,
    bool NotifyOnCardMention = true,
    bool NotifyOnDueSoon = true,
    bool NotifyOnOverdue = true,
    int NotificationRetentionDays = 30,

    // 11. Dashboards & Metric Cards
    bool EnableDashboards = true,
    int MaxDashcardsPerBoard = 10,
    int DashboardRefreshIntervalSeconds = 60,

    // 12. Import & Export
    bool EnableBoardExport = true,
    bool EnableKanbanImport = true,
    int MaxImportFileSizeMb = 50,

    // 13. Search & Indexing
    int SearchMinQueryLength = 2,
    int SearchMaxPageSize = 50,
    bool SearchFuzzyMatching = true,

    // 14. Realtime & Presence
    bool RealtimeBroadcastingEnabled = true,
    bool RealtimePresenceEnabled = true,

    // 15. Card Aging
    bool CardAgingEnabled = true,
    int CardAgingInactiveDays = 14,
    string CardAgingMode = "Regular",

    // 16. Storage & Attachments
    int MaxAttachmentSizeMb = 25,
    string AllowedAttachmentExtensions = "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip",
    bool AllowCoverImages = true,
    int MaxCoverImageSizeMb = 5,

    // 17. Artificial Intelligence
    bool AiEnabled = false,
    string AiProvider = "OpenAiCompatible",
    string AiEndpoint = "http://localhost:11434/",
    string AiModel = "llama3.2",
    string? AiApiKey = null,
    int AiTimeoutSeconds = 60,
    int AiMaxTokens = 2048,

    // 18. Outbound Email (SMTP)
    bool EmailNotificationsEnabled = false,
    string SmtpHost = "localhost",
    int SmtpPort = 587,
    string SmtpUsername = "",
    string? SmtpPassword = null,
    bool SmtpEnableSsl = true,
    string SenderEmail = "noreply@cardscape.local",
    string SenderName = "Cardscape Notificaciones",

    // 19. Inbound Email (Email-to-Board)
    bool InboundEmailEnabled = false,
    string InboundEmailDomain = "inbound.cardscape.local",
    string InboundDefaultList = "Inbox",
    bool InboundAttachSenderEmail = true,

    // 20. Integrations (Slack, GitHub, Calendar, MCP)
    bool SlackIntegrationEnabled = false,
    bool GitHubIntegrationEnabled = false,
    bool GoogleCalendarIntegrationEnabled = false,
    bool McpServerEnabled = true,
    bool CalendarIcsFeedsEnabled = true,
    int CalendarFeedTokenLifetimeDays = 180,

    // 21. Model Context Protocol (MCP Server)
    string McpServerName = "Cardscape-MCP",
    bool McpEnableWriteTools = true,
    int McpMaxBatchSize = 50,

    // 22. Webhooks
    bool WebhooksEnabled = true,
    int MaxWebhookRetries = 3,
    int WebhookTimeoutSeconds = 10,
    bool WebhookPayloadSignatureEnabled = true,

    // 23. API Idempotency
    bool EnableIdempotency = true,
    int IdempotencyReservationWindowMinutes = 15,
    int IdempotencyRetentionWindowHours = 24,

    // 24. Rate Limiting & Performance
    bool RateLimitingEnabled = true,
    int DefaultRequestsPerHour = 1000,
    string RateLimiterBackend = "InMemory",
    int BackgroundJobPollIntervalSeconds = 2,
    int BackgroundJobBatchSize = 10,

    // 25. Infrastructure & Redis
    string? RedisConnectionString = null,
    int RedisDatabase = 0,
    string PendingTotpStoreBackend = "InMemory",
    string PendingTotpStoreKeyPrefix = "cardscape:totp-pending:",
    string RateLimiterKeyPrefix = "cardscape:rl:",

    // 26. Activities & Audit Logging
    bool EnableActivityLogging = true,
    int ActivityPageSize = 25,
    bool AuditLogDetailedIp = true,

    // 27. System Logging (Serilog)
    int LogRetainedFileCountLimit = 30,
    int LogFileSizeLimitMb = 100,

    // 28. Data Retention, Residency & GDPR
    bool RetentionSweeperEnabled = true,
    int SweepIntervalHours = 6,
    int UserGracePeriodDays = 30,
    int ActivityRetentionDays = 365,
    int AuditRetentionDays = 730,
    bool AutoPurgeOrphanAttachments = true,
    bool DataResidencyEnabled = false,
    string DeploymentRegion = "Unspecified",
    bool EnforceDataResidency = false,

    // 29. Seeder & Dev
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
