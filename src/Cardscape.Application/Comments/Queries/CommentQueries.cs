using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Comments.DTOs;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Common;
using MediatR;

namespace Cardscape.Application.Comments.Queries;

public sealed record ListCommentsForCardQuery(Guid CardId) : IRequest<Result<IReadOnlyList<CommentDto>>>;

public sealed class ListCommentsForCardQueryHandler(
    ICommentRepository comments,
    ICurrentUser currentUser) : IRequestHandler<ListCommentsForCardQuery, Result<IReadOnlyList<CommentDto>>>
{
    public async Task<Result<IReadOnlyList<CommentDto>>> Handle(
        ListCommentsForCardQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<CommentDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var items = await comments.ListForCardAsync(new CardId(request.CardId), cancellationToken);
        var rows = items
            .Select(c => new CommentDto(
                c.Id.Value,
                c.CardId.Value,
                c.AuthorId,
                c.Body.Value,
                c.CreatedAt,
                c.UpdatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<CommentDto>>(rows);
    }
}
