namespace Cardscape.Web.Shared;

/// <summary>
/// One column of the <see cref="KanbanBoard{TItem}"/>: a title, an
/// ordered list of cards, and an arbitrary id the caller can use
/// for drop targeting (drag-and-drop is not implemented in this
/// iteration; the API is here so the future DnD can read it from
/// the column).
/// </summary>
public sealed record KanbanColumn<T>(string Id, string Title, IReadOnlyList<T> Cards);
