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

public sealed class GenerateChecklistFromDescriptionCommandHandler(
    ICardRepository cards,
    IBoardRepository boards,
    IBoardListRepository lists,
    ICurrentUser currentUser,
    IAiService ai) : IWolverineHandler
{
    public async Task<Result<AiFeatures.AiGeneratedChecklist>> Handle(
        AiFeatures.GenerateChecklistFromDescriptionCommand request,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result<AiFeatures.AiGeneratedChecklist>.Failure(
                DomainError.Unauthenticated("auth.required", "Authentication is required."));
        }

        // v1.2.0 audit (pass 12): same IDOR as
        // GenerateCardDescription — see that handler.
        var access = await CommentAccessGuard.EnsureCanAccessCardAsync(
            cards, boards, lists, request.CardId, currentUser.Id.Value, ct);
        if (access.IsFailure)
        {
            return Result<AiFeatures.AiGeneratedChecklist>.Failure(access.Error);
        }

        Card? card = await cards.GetByIdAsync(new CardId(request.CardId), ct);
        if (card is null)
        {
            return Result<AiFeatures.AiGeneratedChecklist>.Failure(
                DomainError.NotFound("card.not_found", $"Card {request.CardId} does not exist."));
        }

        Result<AiTextCompletion> result = await ai.CompleteAsync(
            new AiPrompt("make-checklist", card.Description.Value),
            new AiOptions(Temperature: 0.2, MaxTokens: 256),
            ct);

        if (result.IsFailure)
        {
            return Result<AiFeatures.AiGeneratedChecklist>.Failure(result.Error);
        }

        string[] lines = result.Value.Text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.TrimStart('-', '*', ' ', '\t'))
            .Where(l => l.Length > 0)
            .ToArray();

        return Result<AiFeatures.AiGeneratedChecklist>.Success(
            new AiFeatures.AiGeneratedChecklist(lines, result.Value.Model ?? "unknown"));
    }
}
