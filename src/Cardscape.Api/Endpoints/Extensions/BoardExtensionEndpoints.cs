using Cardscape.Application.Extensions;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Extensions;

/// <summary>
/// REST surface for board extensions. Any board member can list
/// extensions; enabling / disabling / updating the config JSON is
/// also open to any member (v0.6.4 — no admin-only gate yet).
/// </summary>
public static class BoardExtensionEndpoints
{
    public static IEndpointRouteBuilder MapBoardExtensionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards/{boardId:guid}/extensions")
            .RequireAuthorization()
            .WithTags("Extensions");

        group.MapGet("/", async (Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardExtensionDto>>>(
                new ListBoardExtensionsQuery(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : DomainErrorResults.ToProblem(result.Error);
        });

        group.MapPost("/", async (
            Guid boardId,
            EnableExtensionBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardExtensionDto>>(
                new EnableBoardExtensionCommand(boardId, (int)body.Kind, body.ConfigJson), ct);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/boards/{boardId}/extensions/{ToRouteValue(body.Kind)}",
                    result.Value)
                : DomainErrorResults.ToProblem(result.Error);
        });

        group.MapDelete("/{kind}", async (Guid boardId, string kind, IMessageBus bus, CancellationToken ct) =>
        {
            if (!TryParseKind(kind, out ExtensionKind parsedKind))
            {
                return InvalidKind(kind);
            }
            var result = await bus.InvokeAsync<Result>(
                new DisableBoardExtensionCommand(boardId, (int)parsedKind), ct);
            return result.IsSuccess ? Results.NoContent() : DomainErrorResults.ToProblem(result.Error);
        });

        // BETA-9-#3 — see test-results/r9/r9-report.md.
        // The list endpoint returns rows with both an `id` (UUID,
        // the row's primary key) and a `kind` (the small int the
        // rest of the API uses as the extension identifier). The
        // PUT/DELETE routes only accept the int, so a caller who
        // grabs the `id` from the GET response and tries to use
        // it as the path segment gets a confusing 404. Accept the
        // UUID here too: look the row up, resolve its kind, and
        // delegate to the integer-route command handler.
        group.MapDelete("/{extensionId:guid}", async (
            Guid boardId,
            Guid extensionId,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var lookup = await bus.InvokeAsync<Result<IReadOnlyList<BoardExtensionDto>>>(
                new ListBoardExtensionsQuery(boardId), ct);
            if (lookup.IsFailure)
            {
                return DomainErrorResults.ToProblem(lookup.Error);
            }

            BoardExtensionDto? row = lookup.Value
                .FirstOrDefault(e => e.Id == extensionId);
            if (row is null)
            {
                return ApiProblemResults.NotFound(
                    "extensions.not_found",
                    "No board extension with that id is enabled on this board.");
            }

            var disable = await bus.InvokeAsync<Result>(
                new DisableBoardExtensionCommand(boardId, row.Kind), ct);
            return disable.IsSuccess ? Results.NoContent() : DomainErrorResults.ToProblem(disable.Error);
        });

        group.MapPut("/{kind}/config", async (
            Guid boardId,
            string kind,
            UpdateConfigBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (!TryParseKind(kind, out ExtensionKind parsedKind))
            {
                return InvalidKind(kind);
            }
            var result = await bus.InvokeAsync<Result<BoardExtensionDto>>(
                new UpdateBoardExtensionConfigCommand(boardId, (int)parsedKind, body.ConfigJson), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : DomainErrorResults.ToProblem(result.Error);
        });

        return app;
    }

    public sealed record EnableExtensionBody(ExtensionKind Kind, string? ConfigJson);
    public sealed record UpdateConfigBody(string? ConfigJson);

    private static bool TryParseKind(string value, out ExtensionKind kind)
    {
        string? name = Enum.GetNames<ExtensionKind>()
            .FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
        return Enum.TryParse(name, out kind);
    }

    private static string ToRouteValue(ExtensionKind kind) =>
        char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString()[1..];

    private static IResult InvalidKind(string kind) => ApiProblemResults.BadRequest(
        "extensions.kind_invalid",
        $"Unknown extension kind '{kind}'. Valid values: {string.Join(", ", Enum.GetValues<ExtensionKind>().Select(ToRouteValue))}.");

}
