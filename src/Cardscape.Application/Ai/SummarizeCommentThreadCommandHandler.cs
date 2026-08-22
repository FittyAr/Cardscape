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
