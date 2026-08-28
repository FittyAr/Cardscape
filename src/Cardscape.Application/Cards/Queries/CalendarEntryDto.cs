using Cardscape.Application.Abstractions.Persistence;

namespace Cardscape.Application.Cards.Queries;

public sealed record CalendarEntryDto(
    Guid CardId,
    Guid ListId,
    string ListName,
    Guid BoardId,
    string BoardName,
    string Title,
    DateTimeOffset DueDate,
    bool IsCompleted)
{
    internal static CalendarEntryDto FromReadModel(CalendarCardReadModel row) => new(
        row.CardId,
        row.ListId,
        string.IsNullOrWhiteSpace(row.ListName) ? $"List {row.ListId.ToString()[..8]}" : row.ListName,
        row.BoardId,
        row.BoardName,
        row.Title,
        row.DueDate,
        row.IsCompleted);
}
