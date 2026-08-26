using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.CustomFields;

public sealed record ListCustomFieldDefinitionsQuery(Guid BoardId) : IMessage;

public static class ListCustomFieldDefinitionsQueryHandler
{
    public static async Task<Result<IReadOnlyList<CustomFieldDefinitionDto>>> Handle(
        ListCustomFieldDefinitionsQuery query,
        ICustomFieldDefinitionRepository definitions,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<CustomFieldDefinitionDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Board? board = await boards.GetWithMembersAsync(
            new BoardId(query.BoardId), cancellationToken);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<CustomFieldDefinitionDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        IReadOnlyList<CustomFieldDefinition> rows =
            await definitions.ListForBoardAsync(board.Id, cancellationToken);
        return Result.Success<IReadOnlyList<CustomFieldDefinitionDto>>(
            rows.Select(CustomFieldDefinitionDto.FromEntity).ToList());
    }
}
