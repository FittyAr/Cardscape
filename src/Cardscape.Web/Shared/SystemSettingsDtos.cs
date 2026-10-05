namespace Cardscape.Web.Shared;

public sealed record SystemSettingsDto(
    // 1. General & Brand
    string InstanceTitle = "Cardscape",
    string SupportEmail = "support@cardscape.local",
    string DefaultLanguage = "es",
    string DefaultTheme = "default",
    bool AllowPublicRegistration = true,

    // 2. Security & Policy
    int JwtAccessTokenMinutes = 1440,
    int PasswordMinLength = 8,
    bool PasswordRequireDigit = true,
    bool PasswordRequireNonAlphanumeric = false,
    bool RequireTwoFactorForAdmins = false,
    int MaxFailedLoginAttempts = 5,

    // 3. Storage & Attachments
    string StorageProvider = "LocalFile",
    string StorageRoot = "Storage",
    int MaxAttachmentSizeMb = 25,
    string AllowedAttachmentExtensions = "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip",

    // 4. Artificial Intelligence
    bool AiEnabled = false,
    string AiProvider = "OpenAiCompatible",
    string AiEndpoint = "http://localhost:11434/",
    string AiModel = "llama3.2",
    string AiApiKeyMasked = "",
    int AiTimeoutSeconds = 60,

    // 5. Outbound Email (SMTP)
    bool EmailNotificationsEnabled = false,
    string SmtpHost = "localhost",
    int SmtpPort = 587,
    string SmtpUsername = "",
    string SmtpPasswordMasked = "",
    bool SmtpEnableSsl = true,
    string SenderEmail = "noreply@cardscape.local",
    string SenderName = "Cardscape Notificaciones",

    // 6. Integrations & Webhooks
    bool WebhooksEnabled = true,
    int MaxWebhookRetries = 3,
    int WebhookTimeoutSeconds = 10,
    bool SlackIntegrationEnabled = false,
    bool GitHubIntegrationEnabled = false,
    bool GoogleCalendarIntegrationEnabled = false,

    // 7. Rate Limiting & Performance
    bool RateLimitingEnabled = true,
    int DefaultRequestsPerHour = 1000,
    string RateLimiterBackend = "InMemory",

    // 8. Data Retention & GDPR
    bool RetentionSweeperEnabled = true,
    int SweepIntervalHours = 6,
    int UserGracePeriodDays = 30,
    int ActivityRetentionDays = 365,
    int AuditRetentionDays = 730,

    // 9. Experimental & Dev
    bool DataResidencyEnabled = false,
    bool SeederEnabled = false,

    // 10. Live Diagnostics (Read-Only)
    string DatabaseProvider = "Sqlite",
    string DatabaseHealth = "Healthy",
    string Environment = "Development",
    string AppVersion = "1.2.0",
    string Uptime = "0m",
    long MemoryUsageMb = 0,
    long FreeDiskSpaceMb = 0);

public sealed record UpdateSystemSettingsRequestDto(
    // 1. General & Brand
    string InstanceTitle = "Cardscape",
    string SupportEmail = "support@cardscape.local",
    string DefaultLanguage = "es",
    string DefaultTheme = "default",
    bool AllowPublicRegistration = true,

    // 2. Security & Policy
    int JwtAccessTokenMinutes = 1440,
    int PasswordMinLength = 8,
    bool PasswordRequireDigit = true,
    bool PasswordRequireNonAlphanumeric = false,
    bool RequireTwoFactorForAdmins = false,
    int MaxFailedLoginAttempts = 5,

    // 3. Storage & Attachments
    int MaxAttachmentSizeMb = 25,
    string AllowedAttachmentExtensions = "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip",

    // 4. Artificial Intelligence
    bool AiEnabled = false,
    string AiProvider = "OpenAiCompatible",
    string AiEndpoint = "http://localhost:11434/",
    string AiModel = "llama3.2",
    string? AiApiKey = null,
    int AiTimeoutSeconds = 60,

    // 5. Outbound Email (SMTP)
    bool EmailNotificationsEnabled = false,
    string SmtpHost = "localhost",
    int SmtpPort = 587,
    string SmtpUsername = "",
    string? SmtpPassword = null,
    bool SmtpEnableSsl = true,
    string SenderEmail = "noreply@cardscape.local",
    string SenderName = "Cardscape Notificaciones",

    // 6. Integrations & Webhooks
    bool WebhooksEnabled = true,
    int MaxWebhookRetries = 3,
    int WebhookTimeoutSeconds = 10,
    bool SlackIntegrationEnabled = false,
    bool GitHubIntegrationEnabled = false,
    bool GoogleCalendarIntegrationEnabled = false,

    // 7. Rate Limiting & Performance
    bool RateLimitingEnabled = true,
    int DefaultRequestsPerHour = 1000,
    string RateLimiterBackend = "InMemory",

    // 8. Data Retention & GDPR
    bool RetentionSweeperEnabled = true,
    int SweepIntervalHours = 6,
    int UserGracePeriodDays = 30,
    int ActivityRetentionDays = 365,
    int AuditRetentionDays = 730,

    // 9. Experimental & Dev
    bool DataResidencyEnabled = false,
    bool SeederEnabled = false);

public sealed record TestEmailRequestDto(string TargetEmail);

public sealed record TestAiResponseDto(bool Success, string Message, string? ModelUsed = null);

public sealed record TestEmailResponseDto(bool Success, string Message);
