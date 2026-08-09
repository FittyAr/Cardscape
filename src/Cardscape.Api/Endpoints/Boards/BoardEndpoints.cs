using Cardscape.Application.Boards.Commands;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Boards.Queries;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Boards;

public sealed record CreateBoardRequestBody(Guid WorkspaceId, string Name, string? Description, BoardVisibility Visibility);

public static class BoardEndpoints
{
    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards").RequireAuthorization().WithTags("Boards");

        group.MapGet("/starred", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardSummaryDto>>>(new ListStarredBoardsQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapGet("/", async (Guid workspaceId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardSummaryDto>>>(new ListBoardsForWorkspaceQuery(workspaceId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapGet("/{boardId:guid}", async (Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardDto>>(new GetBoardQuery(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        // BETA-A3-R2-001 — see
        // test-results/beta/round-2/reports/A3-boards.md.
        // The board lifecycle was missing a delete endpoint
        // (only archive / unarchive existed), so a board
        // couldn't be removed once created. The round-2
        // destructive test plan asked for this and round 1
        // deferred it. The handler is a hard-delete (boards
        // are user-owned content, not audit-required); lists,
        // cards, and attachments cascade via the EF Core
        // cascade rules configured in
        // `CardscapeDbContext.OnModelCreating`.
        group.MapDelete("/{boardId:guid}", async (Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new DeleteBoardCommand(boardId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        group.MapPost("/", async (CreateBoardRequestBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardDto>>(new CreateBoardCommand(
                body.WorkspaceId, body.Name, body.Description, body.Visibility), ct);
            return result.IsSuccess
                ? Results.Created($"/api/boards/{result.Value.Id}", result.Value)
                : MapError(result.Error);
        });

        group.MapPost("/{boardId:guid}/rename", async (Guid boardId, RenameRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            // BETA-7-#7 — see test-results/BETA-TEST-REPORT.md.
            // The DTO accepts BOTH `name` (the consistent
            // shape used by every other create / update
            // endpoint) and the legacy `newName` (kept for
            // backward compatibility with clients written
            // against the v1.0.0 surface). `name` wins when
            // both are supplied so a forward-compatible
            // client never has to think about it.
            string newName = body.Name ?? body.NewName ?? string.Empty;
            var result = await bus.InvokeAsync<Result<BoardDto>>(new RenameBoardCommand(boardId, newName), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{boardId:guid}/description", async (Guid boardId, DescriptionRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            // BETA-7-#7 — accept both `description` and the
            // legacy `newDescription` for back-compat.
            string newDescription = body.Description ?? body.NewDescription ?? string.Empty;
            var result = await bus.InvokeAsync<Result<BoardDto>>(new ChangeBoardDescriptionCommand(boardId, newDescription), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{boardId:guid}/visibility", async (Guid boardId, VisibilityRequest body, IMessageBus bus, CancellationToken ct) =>
        {
            // BETA-7-#7 — accept both `visibility` and the
            // legacy `newVisibility` for back-compat.
            BoardVisibility visibility = body.Visibility ?? body.NewVisibility ?? BoardVisibility.Private;
            var result = await bus.InvokeAsync<Result<BoardDto>>(new ChangeBoardVisibilityCommand(boardId, visibility), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{boardId:guid}/archive", async (Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardDto>>(new ArchiveBoardCommand(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{boardId:guid}/unarchive", async (Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardDto>>(new UnarchiveBoardCommand(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{boardId:guid}/star", async (Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardDto>>(new StarBoardCommand(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapDelete("/{boardId:guid}/star", async (Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<BoardDto>>(new UnstarBoardCommand(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        // BETA-5-#12 — see test-results/BETA-TEST-REPORT.md.
        // No HTTP surface existed for promoting a workspace
        // member to a board member. The Board aggregate had
        // an AddMember method, the application had no command,
        // and the API had no endpoint — so any board mutation
        // that required board membership was effectively
        // un-callable by a workspace member. This endpoint is
        // the seam that closes the loop.
        group.MapPost("/{boardId:guid}/members", async (
            Guid boardId,
            AddBoardMemberBody body,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(
                new AddBoardMemberCommand(boardId, body.UserId, body.Role), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        // BETA-8-API-#1 - see test-results/r8/r8-report.md.
        // The add-member endpoint above (BETA-5-#12) existed
        // since 1.2.0 but no GET counterpart did: the
        // /api/boards/{id}/members URL returned 405. This
        // closes the gap so external integrators (and the
        // future Blazor member panel) can audit board
        // membership. Auth + access mirrors the other board
        // endpoints: the caller must be a member of the
        // board itself.
        group.MapGet("/{boardId:guid}/members", async (
            Guid boardId,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardMemberDto>>>(
                new ListBoardMembersQuery(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        // Export the board as a ZIP archive (board.json + attachments).
        group.MapGet("/{boardId:guid}/export", async (
            Guid boardId,
            [FromServices] Cardscape.Application.Abstractions.Export.IExportService exportService,
            CancellationToken ct) =>
        {
            var result = await exportService.ExportBoardAsync(boardId, ct);
            if (result.IsFailure)
            {
                return MapError(result.Error);
            }

            string fileName = $"board-{boardId}.zip";
            return Results.File(result.Value, "application/zip", fileName);
        });

        // BETA-2-#3 — see test-results/BETA-TEST-REPORT.md.
        //
        // The previous version of this endpoint was
        // .AllowAnonymous()'d with the rationale "public
        // boards are readable by anyone with a link". The
        // effect was that an unauthenticated GET on a
        // PRIVATE board reached the service layer, the
        // service saw `currentUser.Id == null`, and
        // returned DomainError.Unauthenticated which
        // surfaces as 401 — so the endpoint's external
        // contract was "401 for everyone who isn't
        // authenticated, regardless of board visibility".
        // The cleanest fix is to let the standard
        // `RequireAuthorization()` on the parent group gate
        // the request first (so an unauthenticated caller
        // always sees 401 with WWW-Authenticate before any
        // service code runs) and let the service decide
        // 200/403/404 for authenticated callers based on
        // board visibility. Operators that want truly
        // anonymous calendar feeds should expose this
        // endpoint through a separate reverse-proxy rule
        // that injects a service-account JWT.
        group.MapGet("/{boardId:guid}/ics", async (
            Guid boardId,
            [FromServices] Cardscape.Application.Calendar.IIcalendarService calendar,
            CancellationToken ct) =>
        {
            var result = await calendar.RenderBoardAsync(boardId, ct);
            if (result.IsFailure)
            {
                return MapError(result.Error);
            }

            return Results.File(result.Value, "text/calendar", $"board-{boardId}.ics");
        });

        return app;
    }

    public sealed record RenameRequest(string? Name, string? NewName);
    public sealed record DescriptionRequest(string? Description, string? NewDescription);
    public sealed record VisibilityRequest(BoardVisibility? Visibility, BoardVisibility? NewVisibility);
    public sealed record AddBoardMemberBody(Guid UserId, BoardMemberRole Role);

    private static IResult MapError(Cardscape.Domain.Common.DomainError error) => error.Type switch
    {
        Cardscape.Domain.Common.ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        Cardscape.Domain.Common.ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        Cardscape.Domain.Common.ErrorType.Forbidden => Results.Forbid(),
        Cardscape.Domain.Common.ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
