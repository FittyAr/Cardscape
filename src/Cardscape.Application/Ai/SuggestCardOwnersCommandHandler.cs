using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Comments;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Ai;

public sealed class SuggestCardOwnersCommandHandler(
    ICardRepository cards,
    IBoardRepository boards,
    IBoardListRepository lists,
    IUserRepository users,
    ICurrentUser currentUser,
    IAiService ai) : IWolverineHandler
{
    public async Task<Result<AiFeatures.AiOwnerSuggestions>> Handle(
        AiFeatures.SuggestCardOwnersCommand request,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result<AiFeatures.AiOwnerSuggestions>.Failure(
                DomainError.Unauthenticated("auth.required", "Authentication is required."));
        }

        // v1.2.0 audit (pass 12): same IDOR as the other
        // AI endpoints. The previous incarnation also
        // leaked every board member's email to the LLM
        // provider (fixed in pass 4). The board-membership
        // gate is the IDOR half of the fix.
        var access = await CommentAccessGuard.EnsureCanAccessCardAsync(
            cards, boards, lists, request.CardId, currentUser.Id.Value, ct);
        if (access.IsFailure)
        {
            return Result<AiFeatures.AiOwnerSuggestions>.Failure(access.Error);
        }

        Card? card = await cards.GetByIdAsync(new CardId(request.CardId), ct);
        if (card is null)
        {
            return Result<AiFeatures.AiOwnerSuggestions>.Failure(
                DomainError.NotFound("card.not_found", $"Card {request.CardId} does not exist."));
        }

        BoardList? list = await lists.GetByIdAsync(card.ListId, ct);
        if (list is null)
        {
            return Result<AiFeatures.AiOwnerSuggestions>.Failure(
                DomainError.NotFound("list.not_found", "List not found."));
        }

        Board? board = await boards.GetByIdAsync(list.BoardId, ct);
        if (board is null)
        {
            return Result<AiFeatures.AiOwnerSuggestions>.Failure(
                DomainError.NotFound("board.not_found", "Board not found."));
        }

        // Build the candidate list locally — we own the
        // membership data, so the LLM has no business
        // ranking it. The AI's role is limited to
        // producing a "reason" for a human-curated
        // pick. (Previous behaviour fed every member's
        // email to the provider as part of the prompt —
        // an obvious privacy leak when the provider is
        // OpenAI or any third-party LLM.)
        var candidates = new List<(Guid UserId, string DisplayName)>();
        List<Guid> memberIds = board.Members.Select(m => m.UserId).ToList();
        foreach (Guid memberId in memberIds)
        {
            User? user = await users.GetByIdAsync(new UserId(memberId), ct);
            if (user is null)
            {
                continue;
            }
            candidates.Add((user.Id.Value, user.DisplayName.Value));
        }

        if (candidates.Count == 0)
        {
            return Result<AiFeatures.AiOwnerSuggestions>.Success(
                new AiFeatures.AiOwnerSuggestions([], Model: "not-invoked"));
        }

        // The pick is deterministic (the first candidate
        // by membership order) and never leaves the host.
        // The AI is asked only for a short human-readable
        // reason that names the picked member; we do not
        // send emails or other PII to the provider.
        var pick = candidates[0];

        string promptUser =
            $"Card title: {card.Title}\n" +
            $"Board: {board.Name.Value}\n" +
            $"Suggested assignee: {pick.DisplayName}\n" +
            "Write one short sentence explaining why this assignee is a good fit, in the same language as the card title.";

        Result<AiTextCompletion> result = await ai.CompleteAsync(
            new AiPrompt("suggest-owners", promptUser),
            new AiOptions(Temperature: 0.1, MaxTokens: 256),
            ct);

        if (result.IsFailure)
        {
            return Result<AiFeatures.AiOwnerSuggestions>.Failure(result.Error);
        }

        return Result<AiFeatures.AiOwnerSuggestions>.Success(
            new AiFeatures.AiOwnerSuggestions(
                Suggestions:
                [
                    new AiFeatures.AiOwnerSuggestion(
                        UserId: pick.UserId,
                        DisplayName: pick.DisplayName,
                        Reason: result.Value.Text)
                ],
                Model: result.Value.Model ?? "unknown"));
    }
}
