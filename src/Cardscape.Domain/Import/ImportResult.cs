using Cardscape.Domain.Common;

namespace Cardscape.Domain.Import;

/// <summary>
/// Result of an import operation. The default implementation parses
/// a Trello JSON export and creates a matching set of
/// workspaces/boards/lists/cards/labels/members under the target
/// workspace. Other kanban tools can implement an import service
/// (e.g. <c>IImportService</c> in the Application layer) to plug
/// in their own format.
/// </summary>
/// <param name="ImportedWorkspaceIds">
/// IDs of the new workspaces the import created inside the
/// target workspace. Empty when the importer chose
/// to drop the data into the target directly instead of creating
/// new child workspaces.</param>
/// <param name="ImportedBoardIds">IDs of the boards created.</param>
/// <param name="ImportedListIds">IDs of the lists created.</param>
/// <param name="ImportedCardIds">IDs of the cards created.</param>
/// <param name="ImportedLabelIds">IDs of the labels created.</param>
public sealed record ImportResult(
    IReadOnlyList<Guid> ImportedWorkspaceIds,
    IReadOnlyList<Guid> ImportedBoardIds,
    IReadOnlyList<Guid> ImportedListIds,
    IReadOnlyList<Guid> ImportedCardIds,
    IReadOnlyList<Guid> ImportedLabelIds);
