using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Contracts.Settings;
using Cardscape.Domain.Common;
using Cardscape.Infrastructure.Logging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Settings;

/// <summary>
/// File-backed <see cref="ISystemSettingsService"/>. The document lives in
/// <c>{DataRoot}/system_settings.json</c>, is replaced atomically on every
/// write and cached in memory. The AI API key is encrypted with ASP.NET
/// Data Protection and never leaves this class except through
/// <see cref="GetAiApiKeyAsync"/>.
/// </summary>
public sealed class SystemSettingsService : ISystemSettingsService, IDisposable
{
    internal const int SchemaVersion = 2;
    private const string FileName = "system_settings.json";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly IConfiguration _configuration;
    private readonly IDataProtector _protector;
    private readonly ILogger<SystemSettingsService> _logger;
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private StoredSettings? _cached;

    public SystemSettingsService(
        IConfiguration configuration,
        IDataProtectionProvider dataProtection,
        ILogger<SystemSettingsService> logger)
    {
        _configuration = configuration;
        _protector = dataProtection.CreateProtector("Cardscape.SystemSettings.v2");
        _logger = logger;

        string dataDir = configuration["Cardscape:DataRoot"]
            ?? (Directory.Exists("/app/Data") ? "/app/Data" : Path.Combine(Directory.GetCurrentDirectory(), "Data"));
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, FileName);
    }

    public async Task<SystemSettings> GetAsync(CancellationToken ct = default) =>
        (await LoadAsync(ct)).ToPublic();

    public async Task<Result<SystemSettings>> UpdateAsync(
        SystemSettings settings, string? updatedBy, CancellationToken ct = default)
    {
        SystemSettings normalized = Normalize(settings);
        if (normalized.Validate() is { Count: > 0 } errors)
        {
            return Result.Failure<SystemSettings>(DomainError.Validation(
                "settings.invalid", string.Join(" ", errors.Select(e => $"{string.Join(",", e.MemberNames)}: {e.ErrorMessage}"))));
        }

        await _lock.WaitAsync(ct);
        try
        {
            StoredSettings current = await LoadUnlockedAsync(ct);
            string? protectedKey = settings.Ai.ApiKey switch
            {
                null => current.ProtectedAiApiKey,
                "" => null,
                string key => _protector.Protect(key.Trim()),
            };
            StoredSettings updated = new(normalized, protectedKey, DateTimeOffset.UtcNow, updatedBy);
            await SaveUnlockedAsync(updated, ct);
            InfrastructureSettingsLogMessages.SettingsUpdated(_logger, updatedBy ?? "system", normalized.General.InstanceTitle);
            return Result.Success(updated.ToPublic());
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<SystemSettings> ResetAsync(string? resetBy, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            StoredSettings defaults = new(Defaults(), ProtectedAiApiKey: null, DateTimeOffset.UtcNow, resetBy);
            await SaveUnlockedAsync(defaults, ct);
            InfrastructureSettingsLogMessages.SettingsReset(_logger, resetBy ?? "system");
            return defaults.ToPublic();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetAiApiKeyAsync(CancellationToken ct = default)
    {
        StoredSettings stored = await LoadAsync(ct);
        if (stored.ProtectedAiApiKey is null)
        {
            return NullIfBlank(_configuration["Ai:ApiKey"]);
        }

        try
        {
            return _protector.Unprotect(stored.ProtectedAiApiKey);
        }
        catch (CryptographicException ex)
        {
            InfrastructureSettingsLogMessages.AiApiKeyUnreadable(_logger, ex);
            return null;
        }
    }

    public void Dispose() => _lock.Dispose();

    /// <summary>
    /// Settings for a fresh instance. Values that used to live only in
    /// appsettings (AI endpoint, seeder switch) seed the defaults so an
    /// upgrade keeps behaving the same until an administrator edits them.
    /// </summary>
    internal SystemSettings Defaults()
    {
        SystemSettings defaults = new();
        defaults.Ai.Endpoint = NullIfBlank(_configuration["Ai:Endpoint"]) ?? defaults.Ai.Endpoint;
        defaults.Ai.Model = NullIfBlank(_configuration["Ai:Model"]) ?? defaults.Ai.Model;
        defaults.Seeder.Enabled = _configuration.GetValue("Cardscape:Seeder:Enabled", defaultValue: false);
        defaults.Seeder.WipeBeforeSeed = _configuration.GetValue("Cardscape:Seeder:WipeBeforeSeed", defaultValue: false);
        return defaults;
    }

    private async Task<StoredSettings> LoadAsync(CancellationToken ct)
    {
        if (_cached is { } cached)
        {
            return cached;
        }

        await _lock.WaitAsync(ct);
        try
        {
            return await LoadUnlockedAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<StoredSettings> LoadUnlockedAsync(CancellationToken ct)
    {
        if (_cached is { } cached)
        {
            return cached;
        }

        if (!File.Exists(_filePath))
        {
            return _cached = new StoredSettings(Defaults(), null, null, null);
        }

        try
        {
            await using FileStream stream = File.OpenRead(_filePath);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("schemaVersion", out JsonElement version) && version.GetInt32() >= SchemaVersion)
            {
                StoredSettings stored = root.Deserialize<StoredSettings>(JsonOptions)
                    ?? new StoredSettings(Defaults(), null, null, null);
                return _cached = stored with { Settings = Normalize(stored.Settings) };
            }

            // Pre-v2 file: one flat object with ~260 keys and plaintext secrets.
            (SystemSettings migrated, string? legacyKey) = LegacySettingsMigrator.Migrate(root, Defaults());
            StoredSettings upgraded = new(
                Normalize(migrated),
                legacyKey is null ? null : _protector.Protect(legacyKey),
                DateTimeOffset.UtcNow,
                "migration");
            stream.Close();
            await SaveUnlockedAsync(upgraded, ct);
            InfrastructureSettingsLogMessages.LegacySettingsMigrated(_logger, _filePath, SchemaVersion);
            return upgraded;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
        {
            InfrastructureSettingsLogMessages.FailedToReadSettings(_logger, _filePath, ex);
            return _cached = new StoredSettings(Defaults(), null, null, null);
        }
    }

    private async Task SaveUnlockedAsync(StoredSettings stored, CancellationToken ct)
    {
        string tempFile = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(stored, JsonOptions), ct);
        File.Move(tempFile, _filePath, overwrite: true);
        _cached = stored;
    }

    /// <summary>Trims text, turns blank optional text into null and drops write-only fields.</summary>
    internal static SystemSettings Normalize(SystemSettings settings)
    {
        SystemSettings copy = settings.DeepCopy();
        copy.General.InstanceTitle = copy.General.InstanceTitle?.Trim() ?? string.Empty;
        copy.General.SupportEmail = NullIfBlank(copy.General.SupportEmail);
        copy.General.WelcomeMessage = NullIfBlank(copy.General.WelcomeMessage);
        copy.General.LogoUrl = NullIfBlank(copy.General.LogoUrl);
        copy.Notices.AnnouncementMessage = NullIfBlank(copy.Notices.AnnouncementMessage);
        copy.Notices.MaintenanceMessage = NullIfBlank(copy.Notices.MaintenanceMessage);
        copy.Ai.Endpoint = copy.Ai.Endpoint?.Trim() ?? string.Empty;
        copy.Ai.Model = copy.Ai.Model?.Trim() ?? string.Empty;
        copy.Ai.ApiKey = null;
        copy.Ai.HasApiKey = false;
        return copy;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>On-disk document (schema v2).</summary>
    internal sealed record StoredSettings(
        SystemSettings Settings,
        string? ProtectedAiApiKey,
        DateTimeOffset? UpdatedAt,
        string? UpdatedBy)
    {
        public int SchemaVersion { get; init; } = SystemSettingsService.SchemaVersion;

        public SystemSettings ToPublic()
        {
            SystemSettings copy = Settings.DeepCopy();
            copy.Ai.ApiKey = null;
            copy.Ai.HasApiKey = ProtectedAiApiKey is not null;
            return copy;
        }
    }
}
