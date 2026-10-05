using System.Text.Json;
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
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemSettingsService> _logger;
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SystemSettingsDto? _cached;

    public SystemSettingsService(IConfiguration configuration, ILogger<SystemSettingsService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        string dataDir = configuration["Cardscape:DataRoot"]
            ?? (Directory.Exists("/app/Data")
                ? "/app/Data"
                : Path.Combine(Directory.GetCurrentDirectory(), "Data"));

        try
        {
            Directory.CreateDirectory(dataDir);
        }
        catch
        {
            // Best effort; directory may already exist.
        }

        _filePath = Path.Combine(dataDir, "system_settings.json");
    }

    public async Task<SystemSettingsDto> GetSettingsAsync(CancellationToken ct = default)
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

            _cached = await LoadFromFileOrDefaultsAsync(ct);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<SystemSettingsDto> UpdateSettingsAsync(
        UpdateSystemSettingsRequest request,
        string? updatedBy,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            string dbProvider = _configuration["Database:Provider"] ?? "Sqlite";
            string environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
            string storageRoot = _configuration["Storage:LocalRoot"] ?? "Storage";
            string appVersion = "1.2.0";

            var updated = new SystemSettingsDto(
                InstanceTitle: string.IsNullOrWhiteSpace(request.InstanceTitle) ? "Cardscape" : request.InstanceTitle.Trim(),
                AllowPublicRegistration: request.AllowPublicRegistration,
                DefaultLanguage: string.Equals(request.DefaultLanguage, "es", StringComparison.OrdinalIgnoreCase) ? "es" : "en",
                JwtAccessTokenMinutes: Math.Clamp(request.JwtAccessTokenMinutes, 5, 1440),
                DatabaseProvider: dbProvider,
                Environment: environment,
                StorageRoot: storageRoot,
                AppVersion: appVersion);

            string json = JsonSerializer.Serialize(updated, JsonOptions);
            string tempFile = $"{_filePath}.tmp.{Guid.NewGuid():N}";
            await File.WriteAllTextAsync(tempFile, json, ct);
            File.Move(tempFile, _filePath, overwrite: true);

            _cached = updated;
            InfrastructureSettingsLogMessages.SettingsUpdated(_logger, updatedBy ?? "system", updated.InstanceTitle);
            return updated;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> IsPublicRegistrationAllowedAsync(CancellationToken ct = default)
    {
        SystemSettingsDto settings = await GetSettingsAsync(ct);
        return settings.AllowPublicRegistration;
    }

    private async Task<SystemSettingsDto> LoadFromFileOrDefaultsAsync(CancellationToken ct)
    {
        string dbProvider = _configuration["Database:Provider"] ?? "Sqlite";
        string environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
        string storageRoot = _configuration["Storage:LocalRoot"] ?? "Storage";
        string appVersion = "1.2.0";

        if (File.Exists(_filePath))
        {
            try
            {
                string json = await File.ReadAllTextAsync(_filePath, ct);
                var loaded = JsonSerializer.Deserialize<SystemSettingsDto>(json, JsonOptions);
                if (loaded is not null)
                {
                    return loaded with
                    {
                        DatabaseProvider = dbProvider,
                        Environment = environment,
                        StorageRoot = storageRoot,
                        AppVersion = appVersion
                    };
                }
            }
            catch (Exception ex)
            {
                InfrastructureSettingsLogMessages.FailedToReadSettings(_logger, _filePath, ex);
            }
        }

        return new SystemSettingsDto(
            InstanceTitle: "Cardscape",
            AllowPublicRegistration: true,
            DefaultLanguage: "es",
            JwtAccessTokenMinutes: _configuration.GetValue("Jwt:AccessTokenMinutes", 60),
            DatabaseProvider: dbProvider,
            Environment: environment,
            StorageRoot: storageRoot,
            AppVersion: appVersion);
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
