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
/// <param name="Preview">
/// Human-readable summary of the parsed import (counts + sample
/// names). Always populated — the apply path uses it to render
/// a "what got created" summary in the UI, and the dry-run
/// preview path uses it as the only output. Null only on
/// catastrophic parser failure before the structure is
/// understood.</param>
public sealed record ImportResult(
    IReadOnlyList<Guid> ImportedWorkspaceIds,
    IReadOnlyList<Guid> ImportedBoardIds,
    IReadOnlyList<Guid> ImportedListIds,
    IReadOnlyList<Guid> ImportedCardIds,
    IReadOnlyList<Guid> ImportedLabelIds,
    ImportPreview? Preview = null);

/// <summary>
/// Counts and sample names of an import (real or dry-run).
/// Returned by every successful <c>IImportService.ImportTrelloJsonAsync</c>
/// call so the caller (REST endpoint, MCP tool, or Web UI) can
/// show the user what was — or would have been — created.
/// </summary>
/// <param name="BoardCount">Total boards in the parsed file.</param>
/// <param name="ListCount">Total lists across all boards.</param>
/// <param name="CardCount">Total cards across all lists.</param>
/// <param name="LabelCount">Total labels across all boards.</param>
/// <param name="MemberCount">Total members across all boards.</param>
/// <param name="SampleBoardNames">First few board names for display.</param>
/// <param name="SampleListNames">First few list names for display.</param>
/// <param name="SampleCardNames">First few card names for display.</param>
/// <param name="WasApplied">
/// True when the import was actually written to the database.
/// False when the import was a dry-run preview.</param>
public sealed record ImportPreview(
    int BoardCount,
    int ListCount,
    int CardCount,
    int LabelCount,
    int MemberCount,
    IReadOnlyList<string> SampleBoardNames,
    IReadOnlyList<string> SampleListNames,
    IReadOnlyList<string> SampleCardNames,
    bool WasApplied);
