namespace Cardscape.Web.Shared;

/// <summary>
/// One column of the <see cref="KanbanBoard{TItem}"/>: a title,
/// an ordered list of cards, and an arbitrary id the caller
/// can use for drop targeting. The id is the column-side
/// anchor for the drag-and-drop wiring in
/// <c>BoardDetail.razor</c>: the HTML5 dragstart/dragover/drop
/// events carry the source card id in <c>dataTransfer</c>
/// and the destination column id in the column's
/// <c>cs-kanban-column</c> element, so the consumer can
/// translate the drop into a <c>Cards.MoveAsync</c> call
/// without a custom JS interop layer.
/// </summary>
public sealed record KanbanColumn<T>(string Id, string Title, IReadOnlyList<T> Cards);
