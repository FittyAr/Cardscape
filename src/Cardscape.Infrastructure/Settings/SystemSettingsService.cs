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
                InstanceTitle = string.IsNullOrWhiteSpace(request.InstanceTitle) ? "Cardscape" : request.InstanceTitle.Trim(),
                SupportEmail = string.IsNullOrWhiteSpace(request.SupportEmail) ? "support@cardscape.local" : request.SupportEmail.Trim(),
                DefaultLanguage = string.Equals(request.DefaultLanguage, "es", StringComparison.OrdinalIgnoreCase) ? "es" : "en",
                DefaultTheme = string.IsNullOrWhiteSpace(request.DefaultTheme) ? "default" : request.DefaultTheme.Trim(),
                AllowPublicRegistration = request.AllowPublicRegistration,

                JwtAccessTokenMinutes = Math.Clamp(request.JwtAccessTokenMinutes, 5, 43200),
                PasswordMinLength = Math.Clamp(request.PasswordMinLength, 6, 32),
                PasswordRequireDigit = request.PasswordRequireDigit,
                PasswordRequireNonAlphanumeric = request.PasswordRequireNonAlphanumeric,
                RequireTwoFactorForAdmins = request.RequireTwoFactorForAdmins,
                MaxFailedLoginAttempts = Math.Clamp(request.MaxFailedLoginAttempts, 0, 50),

                MaxAttachmentSizeMb = Math.Clamp(request.MaxAttachmentSizeMb, 1, 500),
                AllowedAttachmentExtensions = string.IsNullOrWhiteSpace(request.AllowedAttachmentExtensions) ? "*" : request.AllowedAttachmentExtensions.Trim(),

                AiEnabled = request.AiEnabled,
                AiProvider = string.IsNullOrWhiteSpace(request.AiProvider) ? "OpenAiCompatible" : request.AiProvider.Trim(),
                AiEndpoint = string.IsNullOrWhiteSpace(request.AiEndpoint) ? "http://localhost:11434/" : request.AiEndpoint.Trim(),
                AiModel = string.IsNullOrWhiteSpace(request.AiModel) ? "llama3.2" : request.AiModel.Trim(),
                AiApiKey = resolvedAiApiKey,
                AiTimeoutSeconds = Math.Clamp(request.AiTimeoutSeconds, 5, 300),

                EmailNotificationsEnabled = request.EmailNotificationsEnabled,
                SmtpHost = string.IsNullOrWhiteSpace(request.SmtpHost) ? "localhost" : request.SmtpHost.Trim(),
                SmtpPort = Math.Clamp(request.SmtpPort, 1, 65535),
                SmtpUsername = request.SmtpUsername.Trim(),
                SmtpPassword = resolvedSmtpPassword,
                SmtpEnableSsl = request.SmtpEnableSsl,
                SenderEmail = string.IsNullOrWhiteSpace(request.SenderEmail) ? "noreply@cardscape.local" : request.SenderEmail.Trim(),
                SenderName = string.IsNullOrWhiteSpace(request.SenderName) ? "Cardscape" : request.SenderName.Trim(),

                WebhooksEnabled = request.WebhooksEnabled,
                MaxWebhookRetries = Math.Clamp(request.MaxWebhookRetries, 0, 10),
                WebhookTimeoutSeconds = Math.Clamp(request.WebhookTimeoutSeconds, 1, 60),
                SlackIntegrationEnabled = request.SlackIntegrationEnabled,
                GitHubIntegrationEnabled = request.GitHubIntegrationEnabled,
                GoogleCalendarIntegrationEnabled = request.GoogleCalendarIntegrationEnabled,

                RateLimitingEnabled = request.RateLimitingEnabled,
                DefaultRequestsPerHour = Math.Max(10, request.DefaultRequestsPerHour),
                RateLimiterBackend = string.Equals(request.RateLimiterBackend, "Redis", StringComparison.OrdinalIgnoreCase) ? "Redis" : "InMemory",

                RetentionSweeperEnabled = request.RetentionSweeperEnabled,
                SweepIntervalHours = Math.Clamp(request.SweepIntervalHours, 1, 168),
                UserGracePeriodDays = Math.Clamp(request.UserGracePeriodDays, 1, 365),
                ActivityRetentionDays = Math.Clamp(request.ActivityRetentionDays, 7, 3650),
                AuditRetentionDays = Math.Clamp(request.AuditRetentionDays, 30, 3650),

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
        try
        {
            TimeSpan uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            uptimeStr = uptime.Days > 0
                ? $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
                : $"{uptime.Hours}h {uptime.Minutes}m";
        }
        catch
        {
            // Process start time fallback
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
            InstanceTitle: model.InstanceTitle,
            SupportEmail: model.SupportEmail,
            DefaultLanguage: model.DefaultLanguage,
            DefaultTheme: model.DefaultTheme,
            AllowPublicRegistration: model.AllowPublicRegistration,

            JwtAccessTokenMinutes: model.JwtAccessTokenMinutes,
            PasswordMinLength: model.PasswordMinLength,
            PasswordRequireDigit: model.PasswordRequireDigit,
            PasswordRequireNonAlphanumeric: model.PasswordRequireNonAlphanumeric,
            RequireTwoFactorForAdmins: model.RequireTwoFactorForAdmins,
            MaxFailedLoginAttempts: model.MaxFailedLoginAttempts,

            StorageProvider: "LocalFile",
            StorageRoot: storageRoot,
            MaxAttachmentSizeMb: model.MaxAttachmentSizeMb,
            AllowedAttachmentExtensions: model.AllowedAttachmentExtensions,

            AiEnabled: model.AiEnabled,
            AiProvider: model.AiProvider,
            AiEndpoint: model.AiEndpoint,
            AiModel: model.AiModel,
            AiApiKeyMasked: string.IsNullOrEmpty(model.AiApiKey) ? string.Empty : "******",
            AiTimeoutSeconds: model.AiTimeoutSeconds,

            EmailNotificationsEnabled: model.EmailNotificationsEnabled,
            SmtpHost: model.SmtpHost,
            SmtpPort: model.SmtpPort,
            SmtpUsername: model.SmtpUsername,
            SmtpPasswordMasked: string.IsNullOrEmpty(model.SmtpPassword) ? string.Empty : "******",
            SmtpEnableSsl: model.SmtpEnableSsl,
            SenderEmail: model.SenderEmail,
            SenderName: model.SenderName,

            WebhooksEnabled: model.WebhooksEnabled,
            MaxWebhookRetries: model.MaxWebhookRetries,
            WebhookTimeoutSeconds: model.WebhookTimeoutSeconds,
            SlackIntegrationEnabled: model.SlackIntegrationEnabled,
            GitHubIntegrationEnabled: model.GitHubIntegrationEnabled,
            GoogleCalendarIntegrationEnabled: model.GoogleCalendarIntegrationEnabled,

            RateLimitingEnabled: model.RateLimitingEnabled,
            DefaultRequestsPerHour: model.DefaultRequestsPerHour,
            RateLimiterBackend: model.RateLimiterBackend,

            RetentionSweeperEnabled: model.RetentionSweeperEnabled,
            SweepIntervalHours: model.SweepIntervalHours,
            UserGracePeriodDays: model.UserGracePeriodDays,
            ActivityRetentionDays: model.ActivityRetentionDays,
            AuditRetentionDays: model.AuditRetentionDays,

            DataResidencyEnabled: model.DataResidencyEnabled,
            SeederEnabled: model.SeederEnabled,

            DatabaseProvider: dbProvider,
            DatabaseHealth: "Healthy",
            Environment: environment,
            AppVersion: appVersion,
            Uptime: uptimeStr,
            MemoryUsageMb: memoryMb,
            FreeDiskSpaceMb: freeDiskMb);
    }

    public void Dispose()
    {
        _lock.Dispose();
    }

    private sealed class PersistedSettingsModel
    {
        public string InstanceTitle { get; set; } = "Cardscape";
        public string SupportEmail { get; set; } = "support@cardscape.local";
        public string DefaultLanguage { get; set; } = "es";
        public string DefaultTheme { get; set; } = "default";
        public bool AllowPublicRegistration { get; set; } = true;

        public int JwtAccessTokenMinutes { get; set; } = 1440;
        public int PasswordMinLength { get; set; } = 8;
        public bool PasswordRequireDigit { get; set; } = true;
        public bool PasswordRequireNonAlphanumeric { get; set; }
        public bool RequireTwoFactorForAdmins { get; set; }
        public int MaxFailedLoginAttempts { get; set; } = 5;

        public int MaxAttachmentSizeMb { get; set; } = 25;
        public string AllowedAttachmentExtensions { get; set; } = "png,jpg,jpeg,gif,pdf,txt,docx,xlsx,zip";

        public bool AiEnabled { get; set; }
        public string AiProvider { get; set; } = "OpenAiCompatible";
        public string AiEndpoint { get; set; } = "http://localhost:11434/";
        public string AiModel { get; set; } = "llama3.2";
        public string AiApiKey { get; set; } = string.Empty;
        public int AiTimeoutSeconds { get; set; } = 60;

        public bool EmailNotificationsEnabled { get; set; }
        public string SmtpHost { get; set; } = "localhost";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public bool SmtpEnableSsl { get; set; } = true;
        public string SenderEmail { get; set; } = "noreply@cardscape.local";
        public string SenderName { get; set; } = "Cardscape Notificaciones";

        public bool WebhooksEnabled { get; set; } = true;
        public int MaxWebhookRetries { get; set; } = 3;
        public int WebhookTimeoutSeconds { get; set; } = 10;
        public bool SlackIntegrationEnabled { get; set; }
        public bool GitHubIntegrationEnabled { get; set; }
        public bool GoogleCalendarIntegrationEnabled { get; set; }

        public bool RateLimitingEnabled { get; set; } = true;
        public int DefaultRequestsPerHour { get; set; } = 1000;
        public string RateLimiterBackend { get; set; } = "InMemory";

        public bool RetentionSweeperEnabled { get; set; } = true;
        public int SweepIntervalHours { get; set; } = 6;
        public int UserGracePeriodDays { get; set; } = 30;
        public int ActivityRetentionDays { get; set; } = 365;
        public int AuditRetentionDays { get; set; } = 730;

        public bool DataResidencyEnabled { get; set; }
        public bool SeederEnabled { get; set; }
    }
}
