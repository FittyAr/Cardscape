using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Cardscape.Api.Endpoints.Scim;

/// <summary>
/// SCIM v2 (RFC 7644) <c>/Users</c> + <c>/Groups</c>
/// endpoints. Bearer-token auth is wired in
/// <c>ScimAuthenticationHandler</c>; the resolved workspace
/// id is on <c>HttpContext.Items["scim.workspaceId"]</c> by
/// the time a request lands here.
/// </summary>
public static class ScimEndpoints
{
    // Per RFC 7644 §3.4.2.4, the SCIM `filter` parameter is
    // unbounded. A misbehaving (or hostile) IdP can ship a
    // multi-megabyte string and pin the API process's CPU
    // on repeated substring scans. Cap the filter at 1 KiB —
    // the only shape we recognise today is
    // `userName eq "alice@example.com"`, which fits in ~80
    // bytes; 1 KiB leaves headroom for any future supported
    // predicate (`displayName co "..."`, etc.) without
    // inviting a DoS.
    private const int MaxScimFilterLength = 1024;

    public static IEndpointRouteBuilder MapScimEndpoints(this IEndpointRouteBuilder app)
    {
        // SCIM uses /scim/v2/... — the v2 segment is part of
        // the standard and the IdPs hard-code it. The
        // authorization is the SCIM token (no JWT), so the
        // group is mapped to a custom policy that the
        // ScimAuthenticationHandler satisfies.
        var group = app.MapGroup("/scim/v2").WithTags("SCIM");

        group.MapGet("/Users", async (
            HttpContext http,
            IScimService scim,
            [FromQuery] int? startIndex,
            [FromQuery] int? count,
            [FromQuery] string? filter,
            CancellationToken ct) =>
        {
            if (!TryGetWorkspaceId(http, out Guid workspaceId, out IResult? error))
            {
                return error!;
            }

            if (!string.IsNullOrEmpty(filter) && filter.Length > MaxScimFilterLength)
            {
                return Results.Json(new
                {
                    schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:Error" },
                    status = "400",
                    detail = $"filter is too long (max {MaxScimFilterLength} characters)."
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await scim.ListUsersAsync(
                workspaceId,
                startIndex ?? 1,
                count ?? 50,
                filter,
                ct);
            if (result.IsFailure)
            {
                return MapError(result.Error);
            }

            return Results.Json(new
            {
                schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:ListResponse" },
                totalResults = result.Value.Count,
                Resources = result.Value
            });
        });

        group.MapPost("/Users", async (
            HttpContext http,
            IScimService scim,
            [FromBody] ScimCreateBody body,
            CancellationToken ct) =>
        {
            if (!TryGetWorkspaceId(http, out Guid workspaceId, out IResult? error))
            {
                return error!;
            }

            var req = new ScimUserCreateRequest(
                body.UserName ?? FirstEmailOrEmpty(body.Emails),
                body.Name?.GivenName,
                body.Name?.FamilyName,
                body.Active ?? true,
                body.Password);
            var result = await scim.CreateUserAsync(workspaceId, req, ct);
            return result.IsSuccess
                ? Results.Created($"/scim/v2/Users/{result.Value.Id}", result.Value)
                : MapError(result.Error);
        });

        group.MapGet("/Users/{userId:guid}", async (
            HttpContext http,
            IScimService scim,
            Guid userId,
            CancellationToken ct) =>
        {
            if (!TryGetWorkspaceId(http, out Guid workspaceId, out IResult? error))
            {
                return error!;
            }

            var result = await scim.GetUserAsync(workspaceId, userId, ct);
            return result.IsSuccess ? Results.Json(result.Value) : MapError(result.Error);
        });

        group.MapPut("/Users/{userId:guid}", async (
            HttpContext http,
            IScimService scim,
            Guid userId,
            [FromBody] ScimCreateBody body,
            CancellationToken ct) =>
        {
            if (!TryGetWorkspaceId(http, out Guid workspaceId, out IResult? error))
            {
                return error!;
            }

            var req = new ScimUserCreateRequest(
                body.UserName ?? FirstEmailOrEmpty(body.Emails),
                body.Name?.GivenName,
                body.Name?.FamilyName,
                body.Active ?? true,
                body.Password);
            var result = await scim.ReplaceUserAsync(workspaceId, userId, req, ct);
            return result.IsSuccess ? Results.Json(result.Value) : MapError(result.Error);
        });

        group.MapPatch("/Users/{userId:guid}", async (
            HttpContext http,
            IScimService scim,
            Guid userId,
            [FromBody] ScimPatchBody body,
            CancellationToken ct) =>
        {
            if (!TryGetWorkspaceId(http, out Guid workspaceId, out IResult? error))
            {
                return error!;
            }

            IReadOnlyList<ScimPatchOperation> ops = (body.Operations ?? [])
                .Select(o => new ScimPatchOperation(o.Op, o.Path, o.Value))
                .ToList();
            var result = await scim.PatchUserAsync(workspaceId, userId,
                new ScimPatchRequest(ops), ct);
            return result.IsSuccess ? Results.Json(result.Value) : MapError(result.Error);
        });

        group.MapDelete("/Users/{userId:guid}", async (
            HttpContext http,
            IScimService scim,
            Guid userId,
            CancellationToken ct) =>
        {
            if (!TryGetWorkspaceId(http, out Guid workspaceId, out IResult? error))
            {
                return error!;
            }

            var result = await scim.DeleteUserAsync(workspaceId, userId, ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        // ── /scim/v2/Groups ────────────────────────────────────
        // 1:1 mapping: SCIM Group == Workspace, SCIM Group
        // member == WorkspaceMember. See IScimService for
        // the rationale.

        group.MapGet("/Groups", async (
            HttpContext http,
            IScimService scim,
            [FromQuery] int? startIndex,
            [FromQuery] int? count,
            CancellationToken ct) =>
        {
            if (!TryGetWorkspaceId(http, out Guid workspaceId, out IResult? error))
            {
                return error!;
            }

            ScimListResponse<ScimGroup> response = await scim.ListGroupsAsync(
                workspaceId,
                startIndex ?? 1,
                count ?? 50,
                ct);
            return Results.Json(response);
        });

        group.MapPost("/Groups", async (
            HttpContext http,
            IScimService scim,
            [FromBody] ScimGroupBody body,
            CancellationToken ct) =>
        {
            if (!TryGetWorkspaceId(http, out Guid workspaceId, out IResult? error))
            {
                return error!;
            }

            ScimGroup input = new(
                Id: body.Id ?? string.Empty,
                Schemas: body.Schemas ?? [],
                DisplayName: body.DisplayName ?? string.Empty,
                Members: MapMembers(body.Members));
            var result = await scim.CreateGroupAsync(workspaceId, input, ct);
            return result.IsSuccess
                ? Results.Created($"/scim/v2/Groups/{result.Value.Id}", result.Value)
                : MapError(result.Error);
        });

        group.MapGet("/Groups/{groupId}", async (
            HttpContext http,
            IScimService scim,
            string groupId,
            CancellationToken ct) =>
        {
            if (!TryGetWorkspaceId(http, out Guid workspaceId, out IResult? error))
            {
                return error!;
            }

            var result = await scim.GetGroupAsync(workspaceId, groupId, ct);
            return result.IsSuccess ? Results.Json(result.Value) : MapError(result.Error);
        });

        group.MapPut("/Groups/{groupId}", async (
            HttpContext http,
            IScimService scim,
            string groupId,
            [FromBody] ScimGroupBody body,
            CancellationToken ct) =>
        {
            if (!TryGetWorkspaceId(http, out Guid workspaceId, out IResult? error))
            {
                return error!;
            }

            ScimGroup input = new(
                Id: body.Id ?? groupId,
                Schemas: body.Schemas ?? [],
                DisplayName: body.DisplayName ?? string.Empty,
                Members: MapMembers(body.Members));
            var result = await scim.UpdateGroupAsync(workspaceId, groupId, input, ct);
            return result.IsSuccess ? Results.Json(result.Value) : MapError(result.Error);
        });

        group.MapPatch("/Groups/{groupId}", async (
            HttpContext http,
            IScimService scim,
            string groupId,
            [FromBody] ScimPatchBody body,
            CancellationToken ct) =>
        {
            if (!TryGetWorkspaceId(http, out Guid workspaceId, out IResult? error))
            {
                return error!;
            }

            IReadOnlyList<ScimPatchOperation> ops = (body.Operations ?? [])
                .Select(o => new ScimPatchOperation(o.Op, o.Path, o.Value))
                .ToList();
            var result = await scim.PatchGroupAsync(workspaceId, groupId,
                new ScimPatchRequest(ops), ct);
            return result.IsSuccess ? Results.Json(result.Value) : MapError(result.Error);
        });

        group.MapDelete("/Groups/{groupId}", async (
            HttpContext http,
            IScimService scim,
            string groupId,
            CancellationToken ct) =>
        {
            if (!TryGetWorkspaceId(http, out Guid workspaceId, out IResult? error))
            {
                return error!;
            }

            var result = await scim.DeleteGroupAsync(workspaceId, groupId, ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        return app;
    }

    private static string FirstEmailOrEmpty(IReadOnlyList<ScimEmail>? emails) =>
        emails is { Count: > 0 } ? emails[0].Value ?? string.Empty : string.Empty;

    private static bool TryGetWorkspaceId(HttpContext http, out Guid workspaceId, out IResult? error)
    {
        object? value = http.Items["scim.workspaceId"];
        if (value is Guid id && id != Guid.Empty)
        {
            workspaceId = id;
            error = null;
            return true;
        }

        workspaceId = Guid.Empty;
        error = Results.Json(new
        {
            schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:Error" },
            status = "401",
            detail = "A valid SCIM bearer token is required."
        }, statusCode: StatusCodes.Status401Unauthorized);
        return false;
    }

    private static IResult MapError(DomainError error) => Results.Json(new
    {
        schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:Error" },
        status = ((int)error.Type).ToString(),
        detail = error.Message
    }, statusCode: error.Type switch
    {
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Unauthenticated => StatusCodes.Status401Unauthorized,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    });

    // The SCIM v2 wire shape (subset) used for create +
    // replace. We deserialize into these records rather than
    // touching JsonElement directly so the call sites stay
    // strongly-typed.
    private sealed record ScimCreateBody(
        string? UserName,
        bool? Active,
        string? Password,
        ScimName? Name,
        IReadOnlyList<ScimEmail>? Emails);

    private sealed record ScimName(string? GivenName, string? FamilyName);

    private sealed record ScimEmail(string? Value, bool? Primary);

    private sealed record ScimPatchBody(IReadOnlyList<ScimPatchOperationWire>? Operations);

    private sealed record ScimPatchOperationWire(string Op, string? Path, object? Value);

    // The SCIM v2 wire shape for a Group. `Schemas` and
    // `Id` are informational on input — the service
    // generates a fresh `workspace-{guid}` id on POST and
    // overwrites the `Schemas` field on the way out.
    private sealed record ScimGroupBody(
        string? Id,
        IReadOnlyList<string>? Schemas,
        string? DisplayName,
        IReadOnlyList<ScimGroupMemberBody>? Members);

    private sealed record ScimGroupMemberBody(string? Value, string? Display);

    private static List<ScimGroupMember> MapMembers(
        IReadOnlyList<ScimGroupMemberBody>? source) =>
        source is null
            ? []
            : source
                .Where(m => !string.IsNullOrWhiteSpace(m.Value))
                .Select(m => new ScimGroupMember(m.Value!, m.Display))
                .ToList();
}
