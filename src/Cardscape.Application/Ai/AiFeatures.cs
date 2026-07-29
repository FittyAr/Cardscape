using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Ai;

/// <summary>
/// AI-powered features exposed to the Web UI and the MCP
/// server. Every command is a thin wrapper over
/// <see cref="IAiService"/>; the heavy lifting (prompt
/// composition, persistence) lives in the handlers.
/// </summary>
public static class AiFeatures
{
    // ── Command / response records ────────────────────────────

    public sealed record GenerateCardDescriptionCommand(
        Guid CardId,
        string? ExtraContext = null) : IMessage;

    public sealed record SummarizeCommentThreadCommand(
        IReadOnlyList<Guid> CommentIds) : IMessage;

    public sealed record GenerateChecklistFromDescriptionCommand(
        Guid CardId) : IMessage;

    public sealed record SuggestCardOwnersCommand(
        Guid CardId,
        int MaxSuggestions = 3) : IMessage;

    public sealed record AiGeneratedText(string Text, string Model);
    public sealed record AiGeneratedChecklist(IReadOnlyList<string> Items, string Model);
    public sealed record AiOwnerSuggestion(Guid UserId, string DisplayName, string Reason);
    public sealed record AiOwnerSuggestions(IReadOnlyList<AiOwnerSuggestion> Suggestions, string Model);
}

// ── Handlers ───────────────────────────────────────────────────

public sealed class GenerateCardDescriptionCommandHandler(
    ICardRepository cards,
    IBoardRepository boards,
    IBoardListRepository lists,
    IAiService ai) : IWolverineHandler
{
    public async Task<Result<AiFeatures.AiGeneratedText>> Handle(
        AiFeatures.GenerateCardDescriptionCommand request,
        CancellationToken ct)
    {
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

public sealed class SummarizeCommentThreadCommandHandler(
    ICommentRepository comments,
    IAiService ai) : IWolverineHandler
{
    public async Task<Result<AiFeatures.AiGeneratedText>> Handle(
        AiFeatures.SummarizeCommentThreadCommand request,
        CancellationToken ct)
    {
        if (request.CommentIds.Count == 0)
        {
            return Result<AiFeatures.AiGeneratedText>.Failure(
                DomainError.Validation("comments.empty", "At least one comment id is required."));
        }

        var lines = new List<string>();
        foreach (Guid id in request.CommentIds)
        {
            var comment = await comments.GetByIdAsync(new Domain.Comments.CommentId(id), ct);
            if (comment is null)
            {
                continue;
            }
            lines.Add($"- {comment.Body.Value}");
        }
        if (lines.Count == 0)
        {
            return Result<AiFeatures.AiGeneratedText>.Failure(
                DomainError.NotFound("comments.not_found", "No comments found for the supplied ids."));
        }

        Result<AiTextCompletion> result = await ai.CompleteAsync(
            new AiPrompt("summarize-thread", string.Join("\n", lines)),
            new AiOptions(Temperature: 0.2, MaxTokens: 512),
            ct);

        return result.IsSuccess
            ? Result<AiFeatures.AiGeneratedText>.Success(
                new AiFeatures.AiGeneratedText(result.Value.Text, result.Value.Model ?? "unknown"))
            : Result<AiFeatures.AiGeneratedText>.Failure(result.Error);
    }
}

public sealed class GenerateChecklistFromDescriptionCommandHandler(
    ICardRepository cards,
    IAiService ai) : IWolverineHandler
{
    public async Task<Result<AiFeatures.AiGeneratedChecklist>> Handle(
        AiFeatures.GenerateChecklistFromDescriptionCommand request,
        CancellationToken ct)
    {
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

public sealed class SuggestCardOwnersCommandHandler(
    ICardRepository cards,
    IBoardRepository boards,
    IBoardListRepository lists,
    IUserRepository users,
    IAiService ai) : IWolverineHandler
{
    public async Task<Result<AiFeatures.AiOwnerSuggestions>> Handle(
        AiFeatures.SuggestCardOwnersCommand request,
        CancellationToken ct)
    {
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

        var candidates = new List<string>();
        List<Guid> memberIds = board.Members.Select(m => m.UserId).ToList();
        foreach (Guid memberId in memberIds)
        {
            User? user = await users.GetByIdAsync(new UserId(memberId), ct);
            if (user is null)
            {
                continue;
            }
            candidates.Add($"{user.Id.Value} | {user.DisplayName.Value} | {user.Email.Value}");
        }

        if (candidates.Count == 0)
        {
            return Result<AiFeatures.AiOwnerSuggestions>.Success(
                new AiFeatures.AiOwnerSuggestions([], Model: "rule-based"));
        }

        Result<AiTextCompletion> result = await ai.CompleteAsync(
            new AiPrompt(
                "suggest-owners",
                $"Card: {card.Title}\nCandidates (id | name | email):\n{string.Join("\n", candidates)}"),
            new AiOptions(Temperature: 0.1, MaxTokens: 256),
            ct);

        if (result.IsFailure)
        {
            return Result<AiFeatures.AiOwnerSuggestions>.Failure(result.Error);
        }

        var suggestions = new List<AiFeatures.AiOwnerSuggestion>();
        if (candidates.Count > 0)
        {
            string[] first = candidates[0].Split('|', StringSplitOptions.TrimEntries);
            if (first.Length >= 3 && Guid.TryParse(first[0], out Guid userIdGuid))
            {
                suggestions.Add(new AiFeatures.AiOwnerSuggestion(
                    UserId: userIdGuid,
                    DisplayName: first[1],
                    Reason: result.Value.Text));
            }
        }

        return Result<AiFeatures.AiOwnerSuggestions>.Success(
            new AiFeatures.AiOwnerSuggestions(suggestions, result.Value.Model ?? "unknown"));
    }
}
