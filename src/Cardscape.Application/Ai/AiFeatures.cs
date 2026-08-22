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

public sealed class SummarizeCommentThreadCommandHandler(
    ICommentRepository comments,
    ICardRepository cards,
    IBoardRepository boards,
    IBoardListRepository lists,
    ICurrentUser currentUser,
    IAiService ai) : IWolverineHandler
{
    public async Task<Result<AiFeatures.AiGeneratedText>> Handle(
        AiFeatures.SummarizeCommentThreadCommand request,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result<AiFeatures.AiGeneratedText>.Failure(
                DomainError.Unauthenticated("auth.required", "Authentication is required."));
        }

        if (request.CommentIds.Count == 0)
        {
            return Result<AiFeatures.AiGeneratedText>.Failure(
                DomainError.Validation("comments.empty", "At least one comment id is required."));
        }

        // v1.2.0 audit (pass 12): the previous incarnation
        // had no auth / membership check and a malicious
        // caller could supply a list of comment ids from
        // any card on any board and ask the LLM to
        // summarise them — leaking the comment bodies to
        // the AI provider. The fix is the same card→list
        // →board membership check the comment handlers
        // adopted in the same pass.
        var lines = new List<string>();
        foreach (Guid id in request.CommentIds)
        {
            var comment = await comments.GetByIdAsync(new Domain.Comments.CommentId(id), ct);
            if (comment is null)
            {
                continue;
            }

            var access = await CommentAccessGuard.EnsureCanAccessCardAsync(
                cards, boards, lists, comment.CardId.Value, currentUser.Id.Value, ct);
            if (access.IsFailure)
            {
                // Skip comments the caller cannot see —
                // returning Forbidden would leak the
                // existence of comments on cards the caller
                // has no access to, while still filtering
                // them out of the LLM prompt.
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
