using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Labels.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Wolverine;

namespace Cardscape.Application.Labels.Queries;

public sealed record ListLabelsForBoardQuery(Guid BoardId) : IMessage;

public static class ListLabelsForBoardQueryHandler
{
    public static async Task<Result<IReadOnlyList<LabelDto>>> Handle(
        ListLabelsForBoardQuery query,
        ILabelRepository labels,
        CancellationToken cancellationToken)
    {
        var items = await labels.ListForBoardAsync(new BoardId(query.BoardId), cancellationToken);
        var rows = items
            .Where(l => !l.IsDeleted)
            .Select(l => new LabelDto(
                l.Id.Value,
                l.BoardId.Value,
                l.Name.Value,
                l.Color.Value))
            .ToList();

        return Result.Success<IReadOnlyList<LabelDto>>(rows);
    }
}
