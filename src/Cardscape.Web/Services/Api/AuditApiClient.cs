using System.Globalization;
using Cardscape.Web.Shared;

namespace Cardscape.Web.Services.Api;

/// <summary>Reads the administration audit log: instance-wide (admins) or of one workspace (its managers).</summary>
public interface IAuditApiClient
{
    /// <param name="workspaceId">Null for the instance-wide log.</param>
    Task<ApiResult<AuditEntryPageDto>> ListAsync(
        Guid? workspaceId, AuditQuery query, int page, int pageSize, CancellationToken ct = default);
}

public sealed class AuditApiClient(IHttpClientFactory http) : ApiClientBase(http), IAuditApiClient
{
    public async Task<ApiResult<AuditEntryPageDto>> ListAsync(
        Guid? workspaceId, AuditQuery query, int page, int pageSize, CancellationToken ct = default)
    {
        string path = workspaceId is { } id ? $"api/workspaces/{id}/audit" : "api/admin/audit";
        List<string> parameters = [$"page={page}", $"pageSize={pageSize}"];
        // The date pickers give local calendar days; the API filters on
        // instants, so send the local midnights as offsets ("to" is the
        // exclusive end of the chosen day).
        if (query.From is { } from)
        {
            parameters.Add("from=" + Instant(from.Date));
        }

        if (query.To is { } to)
        {
            parameters.Add("to=" + Instant(to.Date.AddDays(1)));
        }

        if (!string.IsNullOrWhiteSpace(query.ActionPrefix))
        {
            parameters.Add("action=" + Uri.EscapeDataString(query.ActionPrefix));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parameters.Add("search=" + Uri.EscapeDataString(query.Search.Trim()));
        }

        if (query.ActorUserId is { } actor)
        {
            parameters.Add($"actorId={actor}");
        }

        if (query.TargetUserId is { } target)
        {
            parameters.Add($"targetId={target}");
        }

        HttpResponseMessage response = await CreateClient().GetAsync($"{path}?{string.Join('&', parameters)}", ct);
        return await ReadAsync<AuditEntryPageDto>(response, ct);
    }

    private static string Instant(DateTime localDay) =>
        Uri.EscapeDataString(new DateTimeOffset(DateTime.SpecifyKind(localDay, DateTimeKind.Local))
            .ToString("o", CultureInfo.InvariantCulture));
}
