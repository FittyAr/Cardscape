using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Common;
using Cardscape.Domain.Import;

namespace Cardscape.Application.Abstractions.Import;

/// <summary>
/// Imports a board archive from another kanban tool into a
/// Cardscape workspace. The default implementation parses a
/// Trello export (<c>boards.json</c>); other kanban tools can
/// plug in their own <see cref="IImportService"/> by reading
/// their format and producing the same <see cref="ImportResult"/>.
/// </summary>
public interface IImportService
{
    /// <summary>
    /// Parses the JSON stream and writes the matching
    /// workspaces/boards/lists/cards/labels/members into
    /// <paramref name="targetWorkspaceId"/>. The caller (the
    /// REST endpoint or the MCP tool) provides the user's
    /// identity so the importer can author-attribute the
    /// imported rows.
    /// </summary>
    Task<Result<ImportResult>> ImportTrelloJsonAsync(
        Stream json,
        Guid targetWorkspaceId,
        CancellationToken ct = default);
}
