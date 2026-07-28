using Cardscape.Domain.Boards;

namespace Cardscape.Application.Abstractions.Persistence;

public interface IAutomationRuleRepository
{
    /// <summary>Lists every rule the board owns, ordered by <c>Position</c>.</summary>
    Task<IReadOnlyList<BoardAutomationRule>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default);

    /// <summary>Lists every rule the board owns that is currently enabled.</summary>
    Task<IReadOnlyList<BoardAutomationRule>> ListEnabledForBoardAsync(
        BoardId boardId, CancellationToken ct = default);
}
