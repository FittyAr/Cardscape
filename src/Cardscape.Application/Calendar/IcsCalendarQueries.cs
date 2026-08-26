using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Calendar;
using Cardscape.Application.Abstractions.Export;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Calendar;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Calendar;

public sealed record RenderBoardCalendarQuery(Guid BoardId) : IMessage;

public static class RenderBoardCalendarQueryHandler
{
    public static async Task<Result<Stream>> Handle(
        RenderBoardCalendarQuery query,
        ICalendarFeedRenderer calendar,
        CancellationToken ct) =>
        await calendar.RenderBoardAsync(query.BoardId, ct);
}

public sealed record ExportBoardQuery(Guid BoardId) : IMessage;

public static class ExportBoardQueryHandler
{
    public static async Task<Result<Stream>> Handle(
        ExportBoardQuery query,
        Application.Abstractions.Export.IExportService export,
        CancellationToken ct) =>
        await export.ExportBoardAsync(query.BoardId, ct);
}
