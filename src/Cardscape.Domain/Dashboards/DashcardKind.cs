namespace Cardscape.Domain.Dashboards;

/// <summary>The kind of aggregation a <see cref="Dashcard"/> computes.</summary>
public enum DashcardKind
{
    /// <summary>Count of cards past their due date on the board.</summary>
    OverdueCount = 0,
    /// <summary>Cards grouped by assigned member.</summary>
    ByMember = 1,
    /// <summary>Cards grouped by label.</summary>
    ByLabel = 2,
    /// <summary>Cards grouped by list.</summary>
    ByList = 3,
    /// <summary>Cards with a due date in the next 7 days.</summary>
    DueThisWeek = 4
}
