using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Dashboards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Dashboards;
using Wolverine;

namespace Cardscape.Application.Dashboards.Queries;

public sealed record ListDashcardsForBoardQuery(Guid BoardId) : IMessage;

public static class ListDashcardsForBoardQueryHandler
{
    public static async Task<Result<IReadOnlyList<DashcardDto>>> Handle(
        ListDashcardsForBoardQuery query,
        IDashboardRepository repo,
        CancellationToken ct)
    {
        IReadOnlyList<Dashcard> cards = await repo.ListForBoardAsync(new BoardId(query.BoardId), ct);
        IReadOnlyList<DashcardDto> dtos = cards
            .Select(c => new DashcardDto(
                c.Id.Value, c.BoardId.Value, c.Kind, c.Title, c.ConfigurationJson,
                c.Position, c.CreatedAt))
            .ToList();
        return Result.Success(dtos);
    }
}
