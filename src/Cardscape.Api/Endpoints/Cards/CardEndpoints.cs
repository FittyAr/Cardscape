using Cardscape.Application.Cards;
using Cardscape.Application.Cards.Commands;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Cards.Queries;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;
// G6c — disambiguate the two `MirrorCardCommand` records that live
// in the `Cardscape.Application.Cards` namespace (the real one in
// `CardscapeExtensions` that provisions a new `Card` row + a
// `CardMirror` pointer, and the stub in `AdditionalCardCommands`
// that the MCP tool also happens to compile against). We bind the
// canonical one explicitly so the HTTP endpoint and the MCP tool
// agree on the same shape. The matching `MirrorCardResult` is
// nested inside the same static class.
using MirrorCmd = Cardscape.Application.Cards.CardscapeExtensions.MirrorCardCommand;
using MirrorResult = Cardscape.Application.Cards.CardscapeExtensions.MirrorCardResult;
// G6b — disambiguate the snooze commands that live inside the
// `CardscapeExtensions` static class (consolidated command
// bucket). The aliases also make it clear that the endpoint is
// binding the canonical (Wolverine-handler-backed) command and
// not a stray test stub.
using SnoozeCmd = Cardscape.Application.Cards.CardscapeExtensions.SnoozeCardCommand;
using UnsnoozeCmd = Cardscape.Application.Cards.CardscapeExtensions.UnsnoozeCardCommand;

namespace Cardscape.Api.Endpoints.Cards;

public static class CardEndpoints
{
    public static IEndpointRouteBuilder MapCardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cards").RequireAuthorization().WithTags("Cards");

        group.MapGet("/", async (Guid boardId, IMessageBus bus, CancellationToken ct, bool includeArchived = false, bool includeSnoozed = false) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<CardSummaryDto>>>(
                new ListCardsForBoardQuery(boardId, includeArchived, includeSnoozed), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        // Calendar / planner view: cards with a due date in the
        // given range. `boardId` is optional; when null, the query
        // spans every board the caller can read.
        group.MapGet("/calendar", async (
            DateTimeOffset from,
            DateTimeOffset to,
            Guid? boardId,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<CalendarEntryDto>>>(
                new ListCardsDueInRangeQuery(from, to, boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        // Snoozed-cards list for a single board. The Web UI uses
        // this to render the "Show snoozed" toggle; the MCP
        // `cards_list_snoozed` tool calls into the same query
        // (ListSnoozedCardIdsQuery) directly via the bus.
        group.MapGet("/snoozed", async (Guid boardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<Guid>>>(
                new ListSnoozedCardIdsQuery(boardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapGet("/{cardId:guid}", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new GetCardQuery(cardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/", async (CreateCardBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new CreateCardCommand(body.ListId, body.Title, body.Description), ct);
            return result.IsSuccess ? Results.Created($"/api/cards/{result.Value.Id}", result.Value) : MapError(result.Error);
        });

        group.MapPost("/{cardId:guid}/rename", async (Guid cardId, RenameBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new RenameCardCommand(cardId, body.NewTitle), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{cardId:guid}/description", async (Guid cardId, DescriptionBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new ChangeCardDescriptionCommand(cardId, body.NewDescription), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{cardId:guid}/move", async (Guid cardId, MoveBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new MoveCardCommand(cardId, body.NewListId, body.NewPosition), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{cardId:guid}/due-date", async (Guid cardId, DueDateBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new SetCardDueDateCommand(cardId, body.DueDate), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapDelete("/{cardId:guid}/due-date", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new ClearCardDueDateCommand(cardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{cardId:guid}/complete", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new CompleteCardCommand(cardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{cardId:guid}/reopen", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new ReopenCardCommand(cardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{cardId:guid}/archive", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new ArchiveCardCommand(cardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{cardId:guid}/restore", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new RestoreCardCommand(cardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        // BETA-5-#5 — see test-results/BETA-TEST-REPORT.md. The card
        // could only be archived (soft-deleted) and restored before,
        // never truly removed. The UI "Delete" affordance needs a
        // hard delete so the trash bin can be emptied and the
        // list-reordering math doesn't have to special-case archived
        // cards forever.
        group.MapDelete("/{cardId:guid}", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new DeleteCardCommand(cardId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        group.MapPost("/{cardId:guid}/assign/{userId:guid}", async (Guid cardId, Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new AssignCardCommand(cardId, userId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapDelete("/{cardId:guid}/assign/{userId:guid}", async (Guid cardId, Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new UnassignCardCommand(cardId, userId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapPost("/{cardId:guid}/labels/{labelId:guid}", async (Guid cardId, Guid labelId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new AttachLabelToCardCommand(cardId, labelId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        group.MapDelete("/{cardId:guid}/labels/{labelId:guid}", async (Guid cardId, Guid labelId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<CardDto>>(new DetachLabelFromCardCommand(cardId, labelId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        // P3.3 / G6c — mirror the card to a different list. The
        // backing `MirrorCardCommand` (CardscapeExtensions) creates a
        // new `Card` row in the target list and records a
        // `CardMirror` pointer. Returns the new (mirrored) card id
        // so the Web UI can show a success notification and link
        // to the mirrored card.
        group.MapPost("/{cardId:guid}/mirror", async (Guid cardId, MirrorBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<MirrorResult>>(
                new MirrorCmd(cardId, body.TargetListId), ct);
            return result.IsSuccess
                ? Results.Created($"/api/cards/{result.Value.MirrorCardId}", result.Value)
                : MapError(result.Error);
        });

        // Card Snooze (G6b / §3.2). The backing command lives
        // inside CardscapeExtensions (the consolidated command
        // bucket shipped with the G6 vertical slice). The endpoint
        // returns the chosen "until" timestamp so the Web UI can
        // refresh the badge without an extra GET round-trip.
        group.MapPost("/{cardId:guid}/snooze", async (Guid cardId, SnoozeBody body, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<DateTimeOffset>>(
                new SnoozeCmd(cardId, body.Until), ct);
            return result.IsSuccess
                ? Results.Ok(new { until = result.Value })
                : MapError(result.Error);
        });

        group.MapDelete("/{cardId:guid}/snooze", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new UnsnoozeCmd(cardId), ct);
            return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
        });

        return app;
    }

    public sealed record CreateCardBody(Guid ListId, string Title, string? Description);
    public sealed record RenameBody(string NewTitle);
    public sealed record DescriptionBody(string NewDescription);
    public sealed record MoveBody(Guid NewListId, double NewPosition);
    public sealed record DueDateBody(DateTimeOffset DueDate);
    public sealed record MirrorBody(Guid TargetListId);
    public sealed record SnoozeBody(DateTimeOffset Until);

    private static IResult MapError(Cardscape.Domain.Common.DomainError error) => error.Type switch
    {
        Cardscape.Domain.Common.ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        Cardscape.Domain.Common.ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        Cardscape.Domain.Common.ErrorType.Forbidden => Results.Forbid(),
        Cardscape.Domain.Common.ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
