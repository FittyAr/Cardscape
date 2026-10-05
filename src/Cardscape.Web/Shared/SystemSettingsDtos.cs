namespace Cardscape.Web.Shared;

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

    // 4. Workspaces & Boards
    int MaxWorkspacesPerUser = 0,
    int MaxBoardsPerWorkspace = 0,
    int MaxMembersPerWorkspace = 0,
    string DefaultWorkspaceRole = "Member",
    int InvitationExpirationDays = 7,
    bool AllowPublicBoards = true,

    // 5. Cards, Lists & Productivity
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

    // 6. Comments & Collaboration
    bool AllowCommentEditing = true,
    bool AllowCommentDeletion = true,
    int MaxCommentLength = 5000,
    bool AllowUserMentions = true,

    // 7. Labels & Custom Fields
    int MaxLabelsPerBoard = 50,
    int MaxLabelsPerCard = 10,
    bool CustomFieldsEnabled = true,
    int MaxCustomFieldsPerBoard = 30,

    // 8. Board Automation Rules
    bool BoardAutomationEnabled = true,
    int MaxAutomationRulesPerBoard = 20,
    int MaxAutomationActionsPerRule = 5,

    // 9. Notifications & System Alerts
    bool InAppNotificationsEnabled = true,
    int DueSoonThresholdHours = 24,
    bool NotifyOnCardAssignment = true,
    bool NotifyOnCardMention = true,
    bool NotifyOnDueSoon = true,
    bool NotifyOnOverdue = true,
    int NotificationRetentionDays = 30,

    // 10. Search & Indexing
    int SearchMinQueryLength = 2,
    int SearchMaxPageSize = 50,
    bool SearchFuzzyMatching = true,

    // 11. Realtime & Presence
    bool RealtimeBroadcastingEnabled = true,
    bool RealtimePresenceEnabled = true,

    // 12. Card Aging
    bool CardAgingEnabled = true,
    int CardAgingInactiveDays = 14,
    string CardAgingMode = "Regular",

    // 13. Storage & Attachments
    string StorageProvider = "LocalFile",
    string StorageRoot = "Storage",
    int MaxAttachmentSizeMb = 25,
    string AllowedAttachmentExtensions = "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip",
    bool AllowCoverImages = true,
    int MaxCoverImageSizeMb = 5,

    // 14. Artificial Intelligence
    bool AiEnabled = false,
    string AiProvider = "OpenAiCompatible",
    string AiEndpoint = "http://localhost:11434/",
    string AiModel = "llama3.2",
    string AiApiKeyMasked = "",
    int AiTimeoutSeconds = 60,
    int AiMaxTokens = 2048,

    // 15. Outbound Email (SMTP)
    bool EmailNotificationsEnabled = false,
    string SmtpHost = "localhost",
    int SmtpPort = 587,
    string SmtpUsername = "",
    string SmtpPasswordMasked = "",
    bool SmtpEnableSsl = true,
    string SenderEmail = "noreply@cardscape.local",
    string SenderName = "Cardscape Notificaciones",

    // 16. Inbound Email (Email-to-Board)
    bool InboundEmailEnabled = false,
    string InboundEmailDomain = "inbound.cardscape.local",
    string InboundDefaultList = "Inbox",
    bool InboundAttachSenderEmail = true,

    // 17. Integrations (Slack, GitHub, Calendar, MCP)
    bool SlackIntegrationEnabled = false,
    bool GitHubIntegrationEnabled = false,
    bool GoogleCalendarIntegrationEnabled = false,
    bool McpServerEnabled = true,
    bool CalendarIcsFeedsEnabled = true,

    // 18. Webhooks
    bool WebhooksEnabled = true,
    int MaxWebhookRetries = 3,
    int WebhookTimeoutSeconds = 10,
    bool WebhookPayloadSignatureEnabled = true,

    // 19. Rate Limiting & Performance
    bool RateLimitingEnabled = true,
    int DefaultRequestsPerHour = 1000,
    string RateLimiterBackend = "InMemory",
    int BackgroundJobPollIntervalSeconds = 2,
    int BackgroundJobBatchSize = 10,

    // 20. Data Retention & GDPR
    bool RetentionSweeperEnabled = true,
    int SweepIntervalHours = 6,
    int UserGracePeriodDays = 30,
    int ActivityRetentionDays = 365,
    int AuditRetentionDays = 730,
    bool AutoPurgeOrphanAttachments = true,

    // 21. Experimental & Dev
    bool DataResidencyEnabled = false,
    bool SeederEnabled = false,

    // 22. Live Diagnostics (Read-Only)
    string DatabaseProvider = "Sqlite",
    string DatabaseHealth = "Healthy",
    string Environment = "Development",
    string AppVersion = "1.2.0",
    string Uptime = "0m",
    long MemoryUsageMb = 0,
    long FreeDiskSpaceMb = 0,
    int ActiveThreads = 0);

public sealed record UpdateSystemSettingsRequestDto(
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

    // 4. Workspaces & Boards
    int MaxWorkspacesPerUser = 0,
    int MaxBoardsPerWorkspace = 0,
    int MaxMembersPerWorkspace = 0,
    string DefaultWorkspaceRole = "Member",
    int InvitationExpirationDays = 7,
    bool AllowPublicBoards = true,

    // 5. Cards, Lists & Productivity
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

    // 6. Comments & Collaboration
    bool AllowCommentEditing = true,
    bool AllowCommentDeletion = true,
    int MaxCommentLength = 5000,
    bool AllowUserMentions = true,

    // 7. Labels & Custom Fields
    int MaxLabelsPerBoard = 50,
    int MaxLabelsPerCard = 10,
    bool CustomFieldsEnabled = true,
    int MaxCustomFieldsPerBoard = 30,

    // 8. Board Automation Rules
    bool BoardAutomationEnabled = true,
    int MaxAutomationRulesPerBoard = 20,
    int MaxAutomationActionsPerRule = 5,

    // 9. Notifications & System Alerts
    bool InAppNotificationsEnabled = true,
    int DueSoonThresholdHours = 24,
    bool NotifyOnCardAssignment = true,
    bool NotifyOnCardMention = true,
    bool NotifyOnDueSoon = true,
    bool NotifyOnOverdue = true,
    int NotificationRetentionDays = 30,

    // 10. Search & Indexing
    int SearchMinQueryLength = 2,
    int SearchMaxPageSize = 50,
    bool SearchFuzzyMatching = true,

    // 11. Realtime & Presence
    bool RealtimeBroadcastingEnabled = true,
    bool RealtimePresenceEnabled = true,

    // 12. Card Aging
    bool CardAgingEnabled = true,
    int CardAgingInactiveDays = 14,
    string CardAgingMode = "Regular",

    // 13. Storage & Attachments
    int MaxAttachmentSizeMb = 25,
    string AllowedAttachmentExtensions = "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip",
    bool AllowCoverImages = true,
    int MaxCoverImageSizeMb = 5,

    // 14. Artificial Intelligence
    bool AiEnabled = false,
    string AiProvider = "OpenAiCompatible",
    string AiEndpoint = "http://localhost:11434/",
    string AiModel = "llama3.2",
    string? AiApiKey = null,
    int AiTimeoutSeconds = 60,
    int AiMaxTokens = 2048,

    // 15. Outbound Email (SMTP)
    bool EmailNotificationsEnabled = false,
    string SmtpHost = "localhost",
    int SmtpPort = 587,
    string SmtpUsername = "",
    string? SmtpPassword = null,
    bool SmtpEnableSsl = true,
    string SenderEmail = "noreply@cardscape.local",
    string SenderName = "Cardscape Notificaciones",

    // 16. Inbound Email (Email-to-Board)
    bool InboundEmailEnabled = false,
    string InboundEmailDomain = "inbound.cardscape.local",
    string InboundDefaultList = "Inbox",
    bool InboundAttachSenderEmail = true,

    // 17. Integrations (Slack, GitHub, Calendar, MCP)
    bool SlackIntegrationEnabled = false,
    bool GitHubIntegrationEnabled = false,
    bool GoogleCalendarIntegrationEnabled = false,
    bool McpServerEnabled = true,
    bool CalendarIcsFeedsEnabled = true,

    // 18. Webhooks
    bool WebhooksEnabled = true,
    int MaxWebhookRetries = 3,
    int WebhookTimeoutSeconds = 10,
    bool WebhookPayloadSignatureEnabled = true,

    // 19. Rate Limiting & Performance
    bool RateLimitingEnabled = true,
    int DefaultRequestsPerHour = 1000,
    string RateLimiterBackend = "InMemory",
    int BackgroundJobPollIntervalSeconds = 2,
    int BackgroundJobBatchSize = 10,

    // 20. Data Retention & GDPR
    bool RetentionSweeperEnabled = true,
    int SweepIntervalHours = 6,
    int UserGracePeriodDays = 30,
    int ActivityRetentionDays = 365,
    int AuditRetentionDays = 730,
    bool AutoPurgeOrphanAttachments = true,

    // 21. Experimental & Dev
    bool DataResidencyEnabled = false,
    bool SeederEnabled = false);

public sealed record TestEmailRequestDto(string TargetEmail);

public sealed record TestAiResponseDto(bool Success, string Message, string? ModelUsed = null);

public sealed record TestEmailResponseDto(bool Success, string Message);
