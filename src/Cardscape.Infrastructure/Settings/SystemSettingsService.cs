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

            var updated = new PersistedSettingsModel
            {
                // 1. General & Brand
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

                // 2. Security & Policy
                JwtAccessTokenMinutes = Math.Clamp(request.JwtAccessTokenMinutes, 5, 43200),
                PasswordMinLength = Math.Clamp(request.PasswordMinLength, 6, 32),
                PasswordRequireDigit = request.PasswordRequireDigit,
                PasswordRequireNonAlphanumeric = request.PasswordRequireNonAlphanumeric,
                RequireTwoFactorForAdmins = request.RequireTwoFactorForAdmins,
                MaxFailedLoginAttempts = Math.Clamp(request.MaxFailedLoginAttempts, 0, 50),
                LockoutDurationMinutes = Math.Clamp(request.LockoutDurationMinutes, 1, 1440),
                SingleActiveSessionPerUser = request.SingleActiveSessionPerUser,
                MaxApiTokensPerUser = Math.Clamp(request.MaxApiTokensPerUser, 1, 100),
                ApiTokenExpirationDays = Math.Clamp(request.ApiTokenExpirationDays, 1, 365),
                CorsAllowedOrigins = string.IsNullOrWhiteSpace(request.CorsAllowedOrigins) ? "*" : request.CorsAllowedOrigins.Trim(),
                EnforceHttps = request.EnforceHttps,
                EnableSecurityHeaders = request.EnableSecurityHeaders,

                // 3. Enterprise SSO & Provisioning
                SamlSsoEnabled = request.SamlSsoEnabled,
                SamlEnforceForDomain = request.SamlEnforceForDomain.Trim(),
                ScimProvisioningEnabled = request.ScimProvisioningEnabled,
                ScimTokenExpirationDays = Math.Clamp(request.ScimTokenExpirationDays, 1, 365),
                OAuthAppsEnabled = request.OAuthAppsEnabled,
                MaxOAuthAppsPerUser = Math.Clamp(request.MaxOAuthAppsPerUser, 0, 50),

                // 4. Workspaces & Boards
                MaxWorkspacesPerUser = Math.Max(0, request.MaxWorkspacesPerUser),
                MaxBoardsPerWorkspace = Math.Max(0, request.MaxBoardsPerWorkspace),
                MaxMembersPerWorkspace = Math.Max(0, request.MaxMembersPerWorkspace),
                DefaultWorkspaceRole = string.IsNullOrWhiteSpace(request.DefaultWorkspaceRole) ? "Member" : request.DefaultWorkspaceRole.Trim(),
                InvitationExpirationDays = Math.Clamp(request.InvitationExpirationDays, 1, 90),
                AllowPublicBoards = request.AllowPublicBoards,

                // 5. Cards, Lists & Productivity
                DefaultWipLimit = Math.Max(0, request.DefaultWipLimit),
                EnforceWipLimits = request.EnforceWipLimits,
                AllowCardMirroring = request.AllowCardMirroring,
                AllowCardSnoozing = request.AllowCardSnoozing,
                AllowCardVoting = request.AllowCardVoting,
                MaxVotesPerUserPerCard = Math.Clamp(request.MaxVotesPerUserPerCard, 1, 10),
                MaxChecklistsPerCard = Math.Clamp(request.MaxChecklistsPerCard, 1, 50),
                AutoArchiveCompletedCardsDays = Math.Max(0, request.AutoArchiveCompletedCardsDays),
                CardRecurrenceEnabled = request.CardRecurrenceEnabled,
                MaxRecurrenceIntervalDays = Math.Clamp(request.MaxRecurrenceIntervalDays, 1, 3650),

                // 6. Comments & Collaboration
                AllowCommentEditing = request.AllowCommentEditing,
                AllowCommentDeletion = request.AllowCommentDeletion,
                MaxCommentLength = Math.Clamp(request.MaxCommentLength, 100, 50000),
                AllowUserMentions = request.AllowUserMentions,

                // 7. Labels & Custom Fields
                MaxLabelsPerBoard = Math.Max(0, request.MaxLabelsPerBoard),
                MaxLabelsPerCard = Math.Max(0, request.MaxLabelsPerCard),
                CustomFieldsEnabled = request.CustomFieldsEnabled,
                MaxCustomFieldsPerBoard = Math.Max(0, request.MaxCustomFieldsPerBoard),

                // 8. Board Automation Rules
                BoardAutomationEnabled = request.BoardAutomationEnabled,
                MaxAutomationRulesPerBoard = Math.Max(0, request.MaxAutomationRulesPerBoard),
                MaxAutomationActionsPerRule = Math.Clamp(request.MaxAutomationActionsPerRule, 1, 20),

                // 9. Notifications & System Alerts
                InAppNotificationsEnabled = request.InAppNotificationsEnabled,
                DueSoonThresholdHours = Math.Clamp(request.DueSoonThresholdHours, 1, 168),
                NotifyOnCardAssignment = request.NotifyOnCardAssignment,
                NotifyOnCardMention = request.NotifyOnCardMention,
                NotifyOnDueSoon = request.NotifyOnDueSoon,
                NotifyOnOverdue = request.NotifyOnOverdue,
                NotificationRetentionDays = Math.Clamp(request.NotificationRetentionDays, 1, 365),

                // 10. Search & Indexing
                SearchMinQueryLength = Math.Clamp(request.SearchMinQueryLength, 1, 10),
                SearchMaxPageSize = Math.Clamp(request.SearchMaxPageSize, 10, 200),
                SearchFuzzyMatching = request.SearchFuzzyMatching,

                // 11. Realtime & Presence
                RealtimeBroadcastingEnabled = request.RealtimeBroadcastingEnabled,
                RealtimePresenceEnabled = request.RealtimePresenceEnabled,

                // 12. Card Aging
                CardAgingEnabled = request.CardAgingEnabled,
                CardAgingInactiveDays = Math.Clamp(request.CardAgingInactiveDays, 1, 365),
                CardAgingMode = string.IsNullOrWhiteSpace(request.CardAgingMode) ? "Regular" : request.CardAgingMode.Trim(),

                // 13. Storage & Attachments
                MaxAttachmentSizeMb = Math.Clamp(request.MaxAttachmentSizeMb, 1, 500),
                AllowedAttachmentExtensions = string.IsNullOrWhiteSpace(request.AllowedAttachmentExtensions) ? "*" : request.AllowedAttachmentExtensions.Trim(),
                AllowCoverImages = request.AllowCoverImages,
                MaxCoverImageSizeMb = Math.Clamp(request.MaxCoverImageSizeMb, 1, 50),

                // 14. Artificial Intelligence
                AiEnabled = request.AiEnabled,
                AiProvider = string.IsNullOrWhiteSpace(request.AiProvider) ? "OpenAiCompatible" : request.AiProvider.Trim(),
                AiEndpoint = string.IsNullOrWhiteSpace(request.AiEndpoint) ? "http://localhost:11434/" : request.AiEndpoint.Trim(),
                AiModel = string.IsNullOrWhiteSpace(request.AiModel) ? "llama3.2" : request.AiModel.Trim(),
                AiApiKey = resolvedAiApiKey,
                AiTimeoutSeconds = Math.Clamp(request.AiTimeoutSeconds, 5, 300),
                AiMaxTokens = Math.Clamp(request.AiMaxTokens, 128, 32768),

                // 15. Outbound Email (SMTP)
                EmailNotificationsEnabled = request.EmailNotificationsEnabled,
                SmtpHost = string.IsNullOrWhiteSpace(request.SmtpHost) ? "localhost" : request.SmtpHost.Trim(),
                SmtpPort = Math.Clamp(request.SmtpPort, 1, 65535),
                SmtpUsername = request.SmtpUsername.Trim(),
                SmtpPassword = resolvedSmtpPassword,
                SmtpEnableSsl = request.SmtpEnableSsl,
                SenderEmail = string.IsNullOrWhiteSpace(request.SenderEmail) ? "noreply@cardscape.local" : request.SenderEmail.Trim(),
                SenderName = string.IsNullOrWhiteSpace(request.SenderName) ? "Cardscape" : request.SenderName.Trim(),

                // 16. Inbound Email (Email-to-Board)
                InboundEmailEnabled = request.InboundEmailEnabled,
                InboundEmailDomain = string.IsNullOrWhiteSpace(request.InboundEmailDomain) ? "inbound.cardscape.local" : request.InboundEmailDomain.Trim(),
                InboundDefaultList = string.IsNullOrWhiteSpace(request.InboundDefaultList) ? "Inbox" : request.InboundDefaultList.Trim(),
                InboundAttachSenderEmail = request.InboundAttachSenderEmail,

                // 17. Integrations (Slack, GitHub, Calendar, MCP)
                SlackIntegrationEnabled = request.SlackIntegrationEnabled,
                GitHubIntegrationEnabled = request.GitHubIntegrationEnabled,
                GoogleCalendarIntegrationEnabled = request.GoogleCalendarIntegrationEnabled,
                McpServerEnabled = request.McpServerEnabled,
                CalendarIcsFeedsEnabled = request.CalendarIcsFeedsEnabled,

                // 18. Webhooks
                WebhooksEnabled = request.WebhooksEnabled,
                MaxWebhookRetries = Math.Clamp(request.MaxWebhookRetries, 0, 10),
                WebhookTimeoutSeconds = Math.Clamp(request.WebhookTimeoutSeconds, 1, 60),
                WebhookPayloadSignatureEnabled = request.WebhookPayloadSignatureEnabled,

                // 19. Rate Limiting & Performance
                RateLimitingEnabled = request.RateLimitingEnabled,
                DefaultRequestsPerHour = Math.Max(10, request.DefaultRequestsPerHour),
                RateLimiterBackend = string.Equals(request.RateLimiterBackend, "Redis", StringComparison.OrdinalIgnoreCase) ? "Redis" : "InMemory",
                BackgroundJobPollIntervalSeconds = Math.Clamp(request.BackgroundJobPollIntervalSeconds, 1, 60),
                BackgroundJobBatchSize = Math.Clamp(request.BackgroundJobBatchSize, 1, 100),

                // 20. Data Retention & GDPR
                RetentionSweeperEnabled = request.RetentionSweeperEnabled,
                SweepIntervalHours = Math.Clamp(request.SweepIntervalHours, 1, 168),
                UserGracePeriodDays = Math.Clamp(request.UserGracePeriodDays, 1, 365),
                ActivityRetentionDays = Math.Clamp(request.ActivityRetentionDays, 7, 3650),
                AuditRetentionDays = Math.Clamp(request.AuditRetentionDays, 30, 3650),
                AutoPurgeOrphanAttachments = request.AutoPurgeOrphanAttachments,

                // 21. Experimental & Dev
                DataResidencyEnabled = request.DataResidencyEnabled,
                SeederEnabled = request.SeederEnabled
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

        if (string.IsNullOrWhiteSpace(model.AiEndpoint) || !Uri.TryCreate(model.AiEndpoint, UriKind.Absolute, out Uri? uri))
        {
            return new TestAiResponse(false, "El endpoint de IA no es una URL válida.");
        }

        try
        {
            HttpClient client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(Math.Min(10, model.AiTimeoutSeconds));

            if (!string.IsNullOrWhiteSpace(model.AiApiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", model.AiApiKey);
            }

            // Probe endpoint
            HttpResponseMessage response = await client.GetAsync(uri, ct);
            bool success = (int)response.StatusCode < 500; // 200-499 indicates endpoint responds

            InfrastructureSettingsLogMessages.AiTestExecuted(_logger, success);
            return new TestAiResponse(
                success,
                success ? $"Conexión exitosa con el servicio de IA (HTTP {(int)response.StatusCode})." : $"El servicio respondió con error HTTP {(int)response.StatusCode}.",
                model.AiModel);
        }
        catch (Exception ex)
        {
            InfrastructureSettingsLogMessages.AiTestExecuted(_logger, false);
            return new TestAiResponse(false, $"Error al conectar con {model.AiEndpoint}: {ex.Message}", model.AiModel);
        }
    }

    public async Task<TestEmailResponse> TestEmailAsync(string targetEmail, CancellationToken ct = default)
    {
        PersistedSettingsModel model = await GetPersistedModelAsync(ct);
        if (!model.EmailNotificationsEnabled)
        {
            return new TestEmailResponse(false, "Las notificaciones por correo están deshabilitadas en la configuración.");
        }

        if (string.IsNullOrWhiteSpace(model.SmtpHost))
        {
            return new TestEmailResponse(false, "El servidor SMTP no está configurado.");
        }

        try
        {
            using var tcpClient = new TcpClient();
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await tcpClient.ConnectAsync(model.SmtpHost, model.SmtpPort, linked.Token);

            InfrastructureSettingsLogMessages.EmailTestExecuted(_logger, targetEmail, true);
            return new TestEmailResponse(true, $"Conexión exitosa con el servidor SMTP {model.SmtpHost}:{model.SmtpPort}. Mensaje de prueba listo para {targetEmail}.");
        }
        catch (Exception ex)
        {
            InfrastructureSettingsLogMessages.EmailTestExecuted(_logger, targetEmail, false);
            return new TestEmailResponse(false, $"No se pudo establecer conexión con el servidor SMTP ({model.SmtpHost}:{model.SmtpPort}): {ex.Message}");
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
            // 1. General & Brand
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
            CorsAllowedOrigins: model.CorsAllowedOrigins,
            EnforceHttps: model.EnforceHttps,
            EnableSecurityHeaders: model.EnableSecurityHeaders,

            // 3. Enterprise SSO & Provisioning
            SamlSsoEnabled: model.SamlSsoEnabled,
            SamlEnforceForDomain: model.SamlEnforceForDomain,
            ScimProvisioningEnabled: model.ScimProvisioningEnabled,
            ScimTokenExpirationDays: model.ScimTokenExpirationDays,
            OAuthAppsEnabled: model.OAuthAppsEnabled,
            MaxOAuthAppsPerUser: model.MaxOAuthAppsPerUser,

            // 4. Workspaces & Boards
            MaxWorkspacesPerUser: model.MaxWorkspacesPerUser,
            MaxBoardsPerWorkspace: model.MaxBoardsPerWorkspace,
            MaxMembersPerWorkspace: model.MaxMembersPerWorkspace,
            DefaultWorkspaceRole: model.DefaultWorkspaceRole,
            InvitationExpirationDays: model.InvitationExpirationDays,
            AllowPublicBoards: model.AllowPublicBoards,

            // 5. Cards, Lists & Productivity
            DefaultWipLimit: model.DefaultWipLimit,
            EnforceWipLimits: model.EnforceWipLimits,
            AllowCardMirroring: model.AllowCardMirroring,
            AllowCardSnoozing: model.AllowCardSnoozing,
            AllowCardVoting: model.AllowCardVoting,
            MaxVotesPerUserPerCard: model.MaxVotesPerUserPerCard,
            MaxChecklistsPerCard: model.MaxChecklistsPerCard,
            AutoArchiveCompletedCardsDays: model.AutoArchiveCompletedCardsDays,
            CardRecurrenceEnabled: model.CardRecurrenceEnabled,
            MaxRecurrenceIntervalDays: model.MaxRecurrenceIntervalDays,

            // 6. Comments & Collaboration
            AllowCommentEditing: model.AllowCommentEditing,
            AllowCommentDeletion: model.AllowCommentDeletion,
            MaxCommentLength: model.MaxCommentLength,
            AllowUserMentions: model.AllowUserMentions,

            // 7. Labels & Custom Fields
            MaxLabelsPerBoard: model.MaxLabelsPerBoard,
            MaxLabelsPerCard: model.MaxLabelsPerCard,
            CustomFieldsEnabled: model.CustomFieldsEnabled,
            MaxCustomFieldsPerBoard: model.MaxCustomFieldsPerBoard,

            // 8. Board Automation Rules
            BoardAutomationEnabled: model.BoardAutomationEnabled,
            MaxAutomationRulesPerBoard: model.MaxAutomationRulesPerBoard,
            MaxAutomationActionsPerRule: model.MaxAutomationActionsPerRule,

            // 9. Notifications & System Alerts
            InAppNotificationsEnabled: model.InAppNotificationsEnabled,
            DueSoonThresholdHours: model.DueSoonThresholdHours,
            NotifyOnCardAssignment: model.NotifyOnCardAssignment,
            NotifyOnCardMention: model.NotifyOnCardMention,
            NotifyOnDueSoon: model.NotifyOnDueSoon,
            NotifyOnOverdue: model.NotifyOnOverdue,
            NotificationRetentionDays: model.NotificationRetentionDays,

            // 10. Search & Indexing
            SearchMinQueryLength: model.SearchMinQueryLength,
            SearchMaxPageSize: model.SearchMaxPageSize,
            SearchFuzzyMatching: model.SearchFuzzyMatching,

            // 11. Realtime & Presence
            RealtimeBroadcastingEnabled: model.RealtimeBroadcastingEnabled,
            RealtimePresenceEnabled: model.RealtimePresenceEnabled,

            // 12. Card Aging
            CardAgingEnabled: model.CardAgingEnabled,
            CardAgingInactiveDays: model.CardAgingInactiveDays,
            CardAgingMode: model.CardAgingMode,

            // 13. Storage & Attachments
            StorageProvider: "LocalFile",
            StorageRoot: storageRoot,
            MaxAttachmentSizeMb: model.MaxAttachmentSizeMb,
            AllowedAttachmentExtensions: model.AllowedAttachmentExtensions,
            AllowCoverImages: model.AllowCoverImages,
            MaxCoverImageSizeMb: model.MaxCoverImageSizeMb,

            // 14. Artificial Intelligence
            AiEnabled: model.AiEnabled,
            AiProvider: model.AiProvider,
            AiEndpoint: model.AiEndpoint,
            AiModel: model.AiModel,
            AiApiKeyMasked: string.IsNullOrEmpty(model.AiApiKey) ? string.Empty : "******",
            AiTimeoutSeconds: model.AiTimeoutSeconds,
            AiMaxTokens: model.AiMaxTokens,

            // 15. Outbound Email (SMTP)
            EmailNotificationsEnabled: model.EmailNotificationsEnabled,
            SmtpHost: model.SmtpHost,
            SmtpPort: model.SmtpPort,
            SmtpUsername: model.SmtpUsername,
            SmtpPasswordMasked: string.IsNullOrEmpty(model.SmtpPassword) ? string.Empty : "******",
            SmtpEnableSsl: model.SmtpEnableSsl,
            SenderEmail: model.SenderEmail,
            SenderName: model.SenderName,

            // 16. Inbound Email (Email-to-Board)
            InboundEmailEnabled: model.InboundEmailEnabled,
            InboundEmailDomain: model.InboundEmailDomain,
            InboundDefaultList: model.InboundDefaultList,
            InboundAttachSenderEmail: model.InboundAttachSenderEmail,

            // 17. Integrations (Slack, GitHub, Calendar, MCP)
            SlackIntegrationEnabled: model.SlackIntegrationEnabled,
            GitHubIntegrationEnabled: model.GitHubIntegrationEnabled,
            GoogleCalendarIntegrationEnabled: model.GoogleCalendarIntegrationEnabled,
            McpServerEnabled: model.McpServerEnabled,
            CalendarIcsFeedsEnabled: model.CalendarIcsFeedsEnabled,

            // 18. Webhooks
            WebhooksEnabled: model.WebhooksEnabled,
            MaxWebhookRetries: model.MaxWebhookRetries,
            WebhookTimeoutSeconds: model.WebhookTimeoutSeconds,
            WebhookPayloadSignatureEnabled: model.WebhookPayloadSignatureEnabled,

            // 19. Rate Limiting & Performance
            RateLimitingEnabled: model.RateLimitingEnabled,
            DefaultRequestsPerHour: model.DefaultRequestsPerHour,
            RateLimiterBackend: model.RateLimiterBackend,
            BackgroundJobPollIntervalSeconds: model.BackgroundJobPollIntervalSeconds,
            BackgroundJobBatchSize: model.BackgroundJobBatchSize,

            // 20. Data Retention & GDPR
            RetentionSweeperEnabled: model.RetentionSweeperEnabled,
            SweepIntervalHours: model.SweepIntervalHours,
            UserGracePeriodDays: model.UserGracePeriodDays,
            ActivityRetentionDays: model.ActivityRetentionDays,
            AuditRetentionDays: model.AuditRetentionDays,
            AutoPurgeOrphanAttachments: model.AutoPurgeOrphanAttachments,

            // 21. Experimental & Dev
            DataResidencyEnabled: model.DataResidencyEnabled,
            SeederEnabled: model.SeederEnabled,

            // 22. Diagnostics
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
        public string CorsAllowedOrigins { get; set; } = "*";
        public bool EnforceHttps { get; set; } = true;
        public bool EnableSecurityHeaders { get; set; } = true;

        // 3. Enterprise SSO & Provisioning
        public bool SamlSsoEnabled { get; set; }
        public string SamlEnforceForDomain { get; set; } = string.Empty;
        public bool ScimProvisioningEnabled { get; set; }
        public int ScimTokenExpirationDays { get; set; } = 180;
        public bool OAuthAppsEnabled { get; set; } = true;
        public int MaxOAuthAppsPerUser { get; set; } = 5;

        // 4. Workspaces & Boards
        public int MaxWorkspacesPerUser { get; set; }
        public int MaxBoardsPerWorkspace { get; set; }
        public int MaxMembersPerWorkspace { get; set; }
        public string DefaultWorkspaceRole { get; set; } = "Member";
        public int InvitationExpirationDays { get; set; } = 7;
        public bool AllowPublicBoards { get; set; } = true;

        // 5. Cards, Lists & Productivity
        public int DefaultWipLimit { get; set; }
        public bool EnforceWipLimits { get; set; }
        public bool AllowCardMirroring { get; set; } = true;
        public bool AllowCardSnoozing { get; set; } = true;
        public bool AllowCardVoting { get; set; } = true;
        public int MaxVotesPerUserPerCard { get; set; } = 1;
        public int MaxChecklistsPerCard { get; set; } = 10;
        public int AutoArchiveCompletedCardsDays { get; set; }
        public bool CardRecurrenceEnabled { get; set; } = true;
        public int MaxRecurrenceIntervalDays { get; set; } = 365;

        // 6. Comments & Collaboration
        public bool AllowCommentEditing { get; set; } = true;
        public bool AllowCommentDeletion { get; set; } = true;
        public int MaxCommentLength { get; set; } = 5000;
        public bool AllowUserMentions { get; set; } = true;

        // 7. Labels & Custom Fields
        public int MaxLabelsPerBoard { get; set; } = 50;
        public int MaxLabelsPerCard { get; set; } = 10;
        public bool CustomFieldsEnabled { get; set; } = true;
        public int MaxCustomFieldsPerBoard { get; set; } = 30;

        // 8. Board Automation Rules
        public bool BoardAutomationEnabled { get; set; } = true;
        public int MaxAutomationRulesPerBoard { get; set; } = 20;
        public int MaxAutomationActionsPerRule { get; set; } = 5;

        // 9. Notifications & System Alerts
        public bool InAppNotificationsEnabled { get; set; } = true;
        public int DueSoonThresholdHours { get; set; } = 24;
        public bool NotifyOnCardAssignment { get; set; } = true;
        public bool NotifyOnCardMention { get; set; } = true;
        public bool NotifyOnDueSoon { get; set; } = true;
        public bool NotifyOnOverdue { get; set; } = true;
        public int NotificationRetentionDays { get; set; } = 30;

        // 10. Search & Indexing
        public int SearchMinQueryLength { get; set; } = 2;
        public int SearchMaxPageSize { get; set; } = 50;
        public bool SearchFuzzyMatching { get; set; } = true;

        // 11. Realtime & Presence
        public bool RealtimeBroadcastingEnabled { get; set; } = true;
        public bool RealtimePresenceEnabled { get; set; } = true;

        // 12. Card Aging
        public bool CardAgingEnabled { get; set; } = true;
        public int CardAgingInactiveDays { get; set; } = 14;
        public string CardAgingMode { get; set; } = "Regular";

        // 13. Storage & Attachments
        public int MaxAttachmentSizeMb { get; set; } = 25;
        public string AllowedAttachmentExtensions { get; set; } = "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip";
        public bool AllowCoverImages { get; set; } = true;
        public int MaxCoverImageSizeMb { get; set; } = 5;

        // 14. Artificial Intelligence
        public bool AiEnabled { get; set; }
        public string AiProvider { get; set; } = "OpenAiCompatible";
        public string AiEndpoint { get; set; } = "http://localhost:11434/";
        public string AiModel { get; set; } = "llama3.2";
        public string AiApiKey { get; set; } = string.Empty;
        public int AiTimeoutSeconds { get; set; } = 60;
        public int AiMaxTokens { get; set; } = 2048;

        // 15. Outbound Email (SMTP)
        public bool EmailNotificationsEnabled { get; set; }
        public string SmtpHost { get; set; } = "localhost";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public bool SmtpEnableSsl { get; set; } = true;
        public string SenderEmail { get; set; } = "noreply@cardscape.local";
        public string SenderName { get; set; } = "Cardscape Notificaciones";

        // 16. Inbound Email (Email-to-Board)
        public bool InboundEmailEnabled { get; set; }
        public string InboundEmailDomain { get; set; } = "inbound.cardscape.local";
        public string InboundDefaultList { get; set; } = "Inbox";
        public bool InboundAttachSenderEmail { get; set; } = true;

        // 17. Integrations (Slack, GitHub, Calendar, MCP)
        public bool SlackIntegrationEnabled { get; set; }
        public bool GitHubIntegrationEnabled { get; set; }
        public bool GoogleCalendarIntegrationEnabled { get; set; }
        public bool McpServerEnabled { get; set; } = true;
        public bool CalendarIcsFeedsEnabled { get; set; } = true;

        // 18. Webhooks
        public bool WebhooksEnabled { get; set; } = true;
        public int MaxWebhookRetries { get; set; } = 3;
        public int WebhookTimeoutSeconds { get; set; } = 10;
        public bool WebhookPayloadSignatureEnabled { get; set; } = true;

        // 19. Rate Limiting & Performance
        public bool RateLimitingEnabled { get; set; } = true;
        public int DefaultRequestsPerHour { get; set; } = 1000;
        public string RateLimiterBackend { get; set; } = "InMemory";
        public int BackgroundJobPollIntervalSeconds { get; set; } = 2;
        public int BackgroundJobBatchSize { get; set; } = 10;

        // 20. Data Retention & GDPR
        public bool RetentionSweeperEnabled { get; set; } = true;
        public int SweepIntervalHours { get; set; } = 6;
        public int UserGracePeriodDays { get; set; } = 30;
        public int ActivityRetentionDays { get; set; } = 365;
        public int AuditRetentionDays { get; set; } = 730;
        public bool AutoPurgeOrphanAttachments { get; set; } = true;

        // 21. Experimental & Dev
        public bool DataResidencyEnabled { get; set; }
        public bool SeederEnabled { get; set; }
    }
}
