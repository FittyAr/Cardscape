using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Labels.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using MediatR;

namespace Cardscape.Application.Labels.Queries;

public sealed record ListLabelsForBoardQuery(Guid BoardId) : IRequest<Result<IReadOnlyList<LabelDto>>>;

public sealed class ListLabelsForBoardQueryHandler(
    ILabelRepository labels) : IRequestHandler<ListLabelsForBoardQuery, Result<IReadOnlyList<LabelDto>>>
{
    public async Task<Result<IReadOnlyList<LabelDto>>> Handle(
        ListLabelsForBoardQuery request, CancellationToken cancellationToken)
    {
        var items = await labels.ListForBoardAsync(new BoardId(request.BoardId), cancellationToken);
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
