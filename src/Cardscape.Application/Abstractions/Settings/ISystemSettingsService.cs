namespace Cardscape.Application.Abstractions.Settings;

public sealed record SystemSettingsDto(
    // 1. General & Brand
    string InstanceTitle = "Cardscape",
    string SupportEmail = "support@cardscape.local",
    string DefaultLanguage = "es",
    string DefaultTheme = "default",
    bool AllowPublicRegistration = true,
    string WelcomeMessage = "Bienvenido a Cardscape",

    // 2. Security & Policy
    int JwtAccessTokenMinutes = 1440,
    int PasswordMinLength = 8,
    bool PasswordRequireDigit = true,
    bool PasswordRequireNonAlphanumeric = false,
    bool RequireTwoFactorForAdmins = false,
    int MaxFailedLoginAttempts = 5,
    int LockoutDurationMinutes = 15,
    bool SingleActiveSessionPerUser = false,

    // 3. Workspaces & Boards
    int MaxWorkspacesPerUser = 0,
    int MaxBoardsPerWorkspace = 0,
    int MaxMembersPerWorkspace = 0,
    string DefaultWorkspaceRole = "Member",
    int InvitationExpirationDays = 7,
    bool AllowPublicBoards = true,

    // 4. Cards, Lists & Productivity
    int DefaultWipLimit = 0,
    bool EnforceWipLimits = false,
    bool AllowCardMirroring = true,
    bool AllowCardSnoozing = true,
    bool AllowCardVoting = true,
    int MaxVotesPerUserPerCard = 1,
    int MaxChecklistsPerCard = 10,
    int AutoArchiveCompletedCardsDays = 0,

    // 5. Card Aging
    bool CardAgingEnabled = true,
    int CardAgingInactiveDays = 14,
    string CardAgingMode = "Regular",

    // 6. Storage & Attachments
    string StorageProvider = "LocalFile",
    string StorageRoot = "Storage",
    int MaxAttachmentSizeMb = 25,
    string AllowedAttachmentExtensions = "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip",
    bool AllowCoverImages = true,
    int MaxCoverImageSizeMb = 5,

    // 7. Artificial Intelligence
    bool AiEnabled = false,
    string AiProvider = "OpenAiCompatible",
    string AiEndpoint = "http://localhost:11434/",
    string AiModel = "llama3.2",
    string AiApiKeyMasked = "",
    int AiTimeoutSeconds = 60,
    int AiMaxTokens = 2048,

    // 8. Outbound Email (SMTP)
    bool EmailNotificationsEnabled = false,
    string SmtpHost = "localhost",
    int SmtpPort = 587,
    string SmtpUsername = "",
    string SmtpPasswordMasked = "",
    bool SmtpEnableSsl = true,
    string SenderEmail = "noreply@cardscape.local",
    string SenderName = "Cardscape Notificaciones",

    // 9. Inbound Email (Email-to-Board)
    bool InboundEmailEnabled = false,
    string InboundEmailDomain = "inbound.cardscape.local",
    string InboundDefaultList = "Inbox",
    bool InboundAttachSenderEmail = true,

    // 10. Integrations (Slack, GitHub, Calendar, MCP)
    bool SlackIntegrationEnabled = false,
    bool GitHubIntegrationEnabled = false,
    bool GoogleCalendarIntegrationEnabled = false,
    bool McpServerEnabled = true,
    bool CalendarIcsFeedsEnabled = true,

    // 11. Webhooks
    bool WebhooksEnabled = true,
    int MaxWebhookRetries = 3,
    int WebhookTimeoutSeconds = 10,
    bool WebhookPayloadSignatureEnabled = true,

    // 12. Rate Limiting & Performance
    bool RateLimitingEnabled = true,
    int DefaultRequestsPerHour = 1000,
    string RateLimiterBackend = "InMemory",
    int BackgroundJobPollIntervalSeconds = 2,
    int BackgroundJobBatchSize = 10,

    // 13. Data Retention & GDPR
    bool RetentionSweeperEnabled = true,
    int SweepIntervalHours = 6,
    int UserGracePeriodDays = 30,
    int ActivityRetentionDays = 365,
    int AuditRetentionDays = 730,
    bool AutoPurgeOrphanAttachments = true,

    // 14. Experimental & Dev
    bool DataResidencyEnabled = false,
    bool SeederEnabled = false,

    // 15. Live Diagnostics (Read-Only)
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

    // 2. Security & Policy
    int JwtAccessTokenMinutes = 1440,
    int PasswordMinLength = 8,
    bool PasswordRequireDigit = true,
    bool PasswordRequireNonAlphanumeric = false,
    bool RequireTwoFactorForAdmins = false,
    int MaxFailedLoginAttempts = 5,
    int LockoutDurationMinutes = 15,
    bool SingleActiveSessionPerUser = false,

    // 3. Workspaces & Boards
    int MaxWorkspacesPerUser = 0,
    int MaxBoardsPerWorkspace = 0,
    int MaxMembersPerWorkspace = 0,
    string DefaultWorkspaceRole = "Member",
    int InvitationExpirationDays = 7,
    bool AllowPublicBoards = true,

    // 4. Cards, Lists & Productivity
    int DefaultWipLimit = 0,
    bool EnforceWipLimits = false,
    bool AllowCardMirroring = true,
    bool AllowCardSnoozing = true,
    bool AllowCardVoting = true,
    int MaxVotesPerUserPerCard = 1,
    int MaxChecklistsPerCard = 10,
    int AutoArchiveCompletedCardsDays = 0,

    // 5. Card Aging
    bool CardAgingEnabled = true,
    int CardAgingInactiveDays = 14,
    string CardAgingMode = "Regular",

    // 6. Storage & Attachments
    int MaxAttachmentSizeMb = 25,
    string AllowedAttachmentExtensions = "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip",
    bool AllowCoverImages = true,
    int MaxCoverImageSizeMb = 5,

    // 7. Artificial Intelligence
    bool AiEnabled = false,
    string AiProvider = "OpenAiCompatible",
    string AiEndpoint = "http://localhost:11434/",
    string AiModel = "llama3.2",
    string? AiApiKey = null,
    int AiTimeoutSeconds = 60,
    int AiMaxTokens = 2048,

    // 8. Outbound Email (SMTP)
    bool EmailNotificationsEnabled = false,
    string SmtpHost = "localhost",
    int SmtpPort = 587,
    string SmtpUsername = "",
    string? SmtpPassword = null,
    bool SmtpEnableSsl = true,
    string SenderEmail = "noreply@cardscape.local",
    string SenderName = "Cardscape Notificaciones",

    // 9. Inbound Email (Email-to-Board)
    bool InboundEmailEnabled = false,
    string InboundEmailDomain = "inbound.cardscape.local",
    string InboundDefaultList = "Inbox",
    bool InboundAttachSenderEmail = true,

    // 10. Integrations (Slack, GitHub, Calendar, MCP)
    bool SlackIntegrationEnabled = false,
    bool GitHubIntegrationEnabled = false,
    bool GoogleCalendarIntegrationEnabled = false,
    bool McpServerEnabled = true,
    bool CalendarIcsFeedsEnabled = true,

    // 11. Webhooks
    bool WebhooksEnabled = true,
    int MaxWebhookRetries = 3,
    int WebhookTimeoutSeconds = 10,
    bool WebhookPayloadSignatureEnabled = true,

    // 12. Rate Limiting & Performance
    bool RateLimitingEnabled = true,
    int DefaultRequestsPerHour = 1000,
    string RateLimiterBackend = "InMemory",
    int BackgroundJobPollIntervalSeconds = 2,
    int BackgroundJobBatchSize = 10,

    // 13. Data Retention & GDPR
    bool RetentionSweeperEnabled = true,
    int SweepIntervalHours = 6,
    int UserGracePeriodDays = 30,
    int ActivityRetentionDays = 365,
    int AuditRetentionDays = 730,
    bool AutoPurgeOrphanAttachments = true,

    // 14. Experimental & Dev
    bool DataResidencyEnabled = false,
    bool SeederEnabled = false);

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
