using Cardscape.Domain.Common;

namespace Cardscape.Application.Abstractions.Export;

/// <summary>
/// Produces a per-board archive. The default implementation emits
/// a ZIP file with a <c>board.json</c> (the structured export) and
/// every attachment under <c>attachments/</c>. The same shape is
/// read back by the Kanban import path in
/// <c>Cardscape.Infrastructure.Import.KanbanImportService</c>.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Builds a board archive. Returns a <see cref="Stream"/> the
    /// caller is responsible for (the API copies it into the HTTP
    /// response and disposes it).
    /// </summary>
    Task<Result<Stream>> ExportBoardAsync(Guid boardId, CancellationToken ct = default);
}
