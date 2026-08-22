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

public sealed class GenerateCardDescriptionCommandHandler(
    ICardRepository cards,
    IBoardRepository boards,
    IBoardListRepository lists,
    ICurrentUser currentUser,
    IAiService ai) : IWolverineHandler
{
    public async Task<Result<AiFeatures.AiGeneratedText>> Handle(
        AiFeatures.GenerateCardDescriptionCommand request,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result<AiFeatures.AiGeneratedText>.Failure(
                DomainError.Unauthenticated("auth.required", "Authentication is required."));
        }

        // v1.2.0 audit (pass 12): the previous incarnation
        // had no auth / membership check — any authenticated
        // user could ask the LLM to summarise a card on a
        // board they had no business with, leaking the title
        // (and the description, if present) to the AI
        // provider.
        var access = await CommentAccessGuard.EnsureCanAccessCardAsync(
            cards, boards, lists, request.CardId, currentUser.Id.Value, ct);
        if (access.IsFailure)
        {
            return Result<AiFeatures.AiGeneratedText>.Failure(access.Error);
        }

        Card? card = await cards.GetByIdAsync(new CardId(request.CardId), ct);
        if (card is null)
        {
            return Result<AiFeatures.AiGeneratedText>.Failure(
                DomainError.NotFound("card.not_found", $"Card {request.CardId} does not exist."));
        }

        BoardList? list = await lists.GetByIdAsync(card.ListId, ct);
        Board? board = list is null ? null : await boards.GetByIdAsync(list.BoardId, ct);

        string system = "describe-card";
        string user = string.IsNullOrWhiteSpace(request.ExtraContext)
            ? $"Title: {card.Title}\nList: {list?.Name.Value ?? "(unknown)"}\nBoard: {board?.Name.Value ?? "(unknown)"}"
            : $"Title: {card.Title}\nContext: {request.ExtraContext}";

        Result<AiTextCompletion> result = await ai.CompleteAsync(
            new AiPrompt(system, user),
            new AiOptions(Temperature: 0.4, MaxTokens: 256),
            ct);

        return result.IsSuccess
            ? Result<AiFeatures.AiGeneratedText>.Success(
                new AiFeatures.AiGeneratedText(result.Value.Text, result.Value.Model ?? "unknown"))
            : Result<AiFeatures.AiGeneratedText>.Failure(result.Error);
    }
}
