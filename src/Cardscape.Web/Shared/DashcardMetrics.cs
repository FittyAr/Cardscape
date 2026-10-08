namespace Cardscape.Web.Shared;

/// <summary>One bar of a distribution widget (cards per list / person / label).</summary>
public sealed record DashcardBucket(string Label, int Count, string? Color = null, bool IsUnassigned = false);

/// <summary>What a dashcard displays: a headline number, the cards behind it
/// (for the date widgets) and the bars (for the distribution widgets).</summary>
public sealed record DashcardResult(
    int Total,
    IReadOnlyList<CardSummaryDto> Cards,
    IReadOnlyList<DashcardBucket> Buckets);

/// <summary>
/// Computes the board dashboard widgets from the board's open cards. The API
/// stores only the widget definitions (kind, title, position); the numbers
/// are derived here from the same card summaries the kanban view loads.
/// Completed cards are left out everywhere: the dashboard tracks open work.
/// </summary>
public static class DashcardMetrics
{
    /// <summary>How many cards the date widgets list under their number.</summary>
    public const int ListedCards = 5;

    /// <param name="unassignedLabel">Bucket label for cards without a member / label.</param>
    public static DashcardResult Compute(
        DashcardKind kind,
        IReadOnlyList<CardSummaryDto> cards,
        IReadOnlyList<BoardListDto> lists,
        DateTimeOffset now,
        string unassignedLabel)
    {
        List<CardSummaryDto> open = cards.Where(card => !card.IsCompleted && !card.IsArchived).ToList();
        return kind switch
        {
            DashcardKind.OverdueCount => Dated(open.Where(card => card.DueDate < now)),
            DashcardKind.DueThisWeek => Dated(open.Where(card => card.DueDate >= now && card.DueDate < now.AddDays(7))),
            DashcardKind.ByList => Distribution(open, ByList(open, lists)),
            DashcardKind.ByMember => Distribution(open, ByMember(open, unassignedLabel)),
            DashcardKind.ByLabel => Distribution(open, ByLabel(open, unassignedLabel)),
            _ => new DashcardResult(0, [], []),
        };
    }

    private static DashcardResult Dated(IEnumerable<CardSummaryDto> cards)
    {
        List<CardSummaryDto> ordered = cards.OrderBy(card => card.DueDate).ToList();
        return new DashcardResult(ordered.Count, ordered.Take(ListedCards).ToList(), []);
    }

    private static DashcardResult Distribution(List<CardSummaryDto> open, IReadOnlyList<DashcardBucket> buckets) =>
        new(open.Count, [], buckets);

    // Every open list, in board order, even when empty: an empty column is
    // information too.
    private static List<DashcardBucket> ByList(List<CardSummaryDto> open, IReadOnlyList<BoardListDto> lists) =>
        lists.Where(list => !list.IsArchived)
            .OrderBy(list => list.Position)
            .Select(list => new DashcardBucket(list.Name, open.Count(card => card.ListId == list.Id)))
            .ToList();

    private static List<DashcardBucket> ByMember(List<CardSummaryDto> open, string unassignedLabel)
    {
        List<DashcardBucket> buckets = open
            .SelectMany(card => card.Members ?? [])
            .GroupBy(member => member.UserId)
            .Select(group => new DashcardBucket(group.First().DisplayName, group.Count()))
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return WithUnassigned(buckets, open.Count(card => card.Members is not { Count: > 0 }), unassignedLabel);
    }

    private static List<DashcardBucket> ByLabel(List<CardSummaryDto> open, string unassignedLabel)
    {
        List<DashcardBucket> buckets = open
            .SelectMany(card => card.Labels ?? [])
            .GroupBy(label => label.Id)
            .Select(group => new DashcardBucket(group.First().Name, group.Count(), group.First().Color))
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return WithUnassigned(buckets, open.Count(card => card.Labels is not { Count: > 0 }), unassignedLabel);
    }

    private static List<DashcardBucket> WithUnassigned(List<DashcardBucket> buckets, int count, string label)
    {
        if (count > 0)
        {
            buckets.Add(new DashcardBucket(label, count, IsUnassigned: true));
        }

        return buckets;
    }
}
