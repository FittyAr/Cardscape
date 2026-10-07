using System.Diagnostics;
using System.Reflection;
using Cardscape.Contracts.Settings;
using Cardscape.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Cardscape.Api.Settings;

/// <summary>Live health snapshot of the running instance (nothing here is hard-coded).</summary>
public sealed class SystemDiagnosticsProbe(
    CardscapeDbContext db,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    public async Task<SystemDiagnostics> ProbeAsync(CancellationToken ct)
    {
        using Process process = Process.GetCurrentProcess();
        return new SystemDiagnostics(
            AppVersion: Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown",
            Environment: environment.EnvironmentName,
            DatabaseProvider: configuration["Database:Provider"] ?? "Sqlite",
            DatabaseReachable: await CanConnectAsync(ct),
            Uptime: DateTime.Now - process.StartTime,
            WorkingSetMb: process.WorkingSet64 / (1024 * 1024),
            FreeDiskSpaceMb: FreeDiskSpaceMb(environment.ContentRootPath),
            ThreadCount: process.Threads.Count);
    }

    private async Task<bool> CanConnectAsync(CancellationToken ct)
    {
        try
        {
            return await db.Database.CanConnectAsync(ct);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static long FreeDiskSpaceMb(string path)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace / (1024 * 1024);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return -1;
        }
    }
}
