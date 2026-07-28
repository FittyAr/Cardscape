using Cardscape.Domain.Boards;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>
/// Read/write repository for <see cref="BoardExtension"/>.
/// The (boardId, kind) tuple is unique: re-enabling an extension
/// should be a no-op rather than creating a duplicate row.
/// </summary>
public interface IBoardExtensionRepository : IRepository<BoardExtension, BoardExtensionId>
{
    /// <summary>Lists every extension (enabled or disabled) for a board.</summary>
    Task<IReadOnlyList<BoardExtension>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default);

    /// <summary>Looks up a single extension by its board + kind tuple.</summary>
    Task<BoardExtension?> GetByBoardAndKindAsync(
        BoardId boardId, ExtensionKind kind, CancellationToken ct = default);
}
