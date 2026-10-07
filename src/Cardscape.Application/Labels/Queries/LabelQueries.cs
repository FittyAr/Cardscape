using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Labels.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Labels.Queries;

public sealed record ListLabelsForBoardQuery(Guid BoardId) : IMessage;

public static class ListLabelsForBoardQueryHandler
{
    public static async Task<Result<IReadOnlyList<LabelDto>>> HandleAsync(
        ListLabelsForBoardQuery query,
        ILabelRepository labels,
        CancellationToken cancellationToken)
    {
        var items = await labels.ListForBoardAsync(new BoardId(query.BoardId), cancellationToken);
        var rows = items
            .Where(l => !l.IsDeleted)
            .Select(LabelDto.FromEntity)
            .ToList();

        return Result.Success<IReadOnlyList<LabelDto>>(rows);
    }
}
