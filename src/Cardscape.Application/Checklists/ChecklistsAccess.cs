using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Application.Checklists;

/// <summary>Internal helper that wraps the membership-check pattern
/// shared by all per-checklist operations.</summary>
internal static class ChecklistsAccess
{
    /// <summary>
    /// Loads the checklist, follows card→list→board, and verifies
    /// the current user is a board member. The v1.2.0 audit
    /// (pass 12) found that the rename / delete / item handlers
    /// accepted any <c>checklistId</c> from any authenticated
    /// user — a clear IDOR. The handler chain now goes
    /// handler → load checklist → this guard → aggregate
    /// mutation, mirroring the pattern the comment handlers
    /// adopted in the same pass.
    /// </summary>
    public static async Task<Result> EnsureCanAccessChecklistAsync(
        Guid checklistId,
        IChecklistRepository checklists,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Checklist? checklist = await checklists.GetByIdAsync(new ChecklistId(checklistId), ct);
        if (checklist is null)
        {
            return Result.Failure(DomainError.NotFound(
                "checklists.not_found", "Checklist was not found."));
        }

        Card? card = await cards.GetByIdAsync(checklist.CardId, ct);
        if (card is null)
        {
            return Result.Failure(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        BoardId? boardId = await lists.GetBoardIdAsync(card.ListId, ct);
        if (boardId is null)
        {
            return Result.Failure(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(boardId, ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        return Result.Success();
    }
}
