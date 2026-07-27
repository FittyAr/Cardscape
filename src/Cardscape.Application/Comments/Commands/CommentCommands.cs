using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Comments.DTOs;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Common;
using Wolverine;
using static Cardscape.Domain.Comments.Errors.CommentErrors;

namespace Cardscape.Application.Comments.Commands;

public sealed record AddCommentCommand(Guid CardId, string Body) : IMessage;

public static class AddCommentCommandHandler
{
    public static async Task<Result<CommentDto>> Handle(
        AddCommentCommand command,
        ICardRepository cards,
        ICommentRepository comments,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CommentDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var card = await cards.GetByIdAsync(new CardId(command.CardId), cancellationToken);
        if (card is null)
        {
            return Result.Failure<CommentDto>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        var bodyResult = CommentBody.Create(command.Body);
        if (bodyResult.IsFailure)
        {
            return Result.Failure<CommentDto>(bodyResult.Error);
        }

        var commentResult = Comment.Create(
            CommentId.New(),
            new CardId(command.CardId),
            currentUser.Id.Value,
            bodyResult.Value,
            clock.UtcNow);

        if (commentResult.IsFailure)
        {
            return Result.Failure<CommentDto>(commentResult.Error);
        }

        await comments.AddAsync(commentResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CommentDto(
            commentResult.Value.Id.Value,
            commentResult.Value.CardId.Value,
            commentResult.Value.AuthorId,
            commentResult.Value.Body.Value,
            commentResult.Value.CreatedAt,
            commentResult.Value.UpdatedAt));
    }
}

public sealed record EditCommentCommand(Guid CommentId, string NewBody) : IMessage;

public static class EditCommentCommandHandler
{
    public static async Task<Result<CommentDto>> Handle(
        EditCommentCommand command,
        ICommentRepository comments,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<CommentDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var comment = await comments.GetByIdAsync(new CommentId(command.CommentId), cancellationToken);
        if (comment is null)
        {
            return Result.Failure<CommentDto>(NotFound);
        }

        var bodyResult = CommentBody.Create(command.NewBody);
        if (bodyResult.IsFailure)
        {
            return Result.Failure<CommentDto>(bodyResult.Error);
        }

        var editResult = comment.Edit(bodyResult.Value, currentUser.Id.Value, clock.UtcNow);
        if (editResult.IsFailure)
        {
            return Result.Failure<CommentDto>(editResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new CommentDto(
            comment.Id.Value, comment.CardId.Value, comment.AuthorId,
            comment.Body.Value, comment.CreatedAt, comment.UpdatedAt));
    }
}

public sealed record DeleteCommentCommand(Guid CommentId) : IMessage;

public static class DeleteCommentCommandHandler
{
    public static async Task<Result> Handle(
        DeleteCommentCommand command,
        ICommentRepository comments,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var comment = await comments.GetByIdAsync(new CommentId(command.CommentId), cancellationToken);
        if (comment is null)
        {
            return Result.Failure(NotFound);
        }

        var result = comment.Delete(currentUser.Id.Value, clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
