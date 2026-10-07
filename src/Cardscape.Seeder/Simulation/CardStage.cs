namespace Cardscape.Seeder.Simulation;

/// <summary>How far through the board's workflow a simulated card got.</summary>
public enum CardStage
{
    Backlog = 0,
    Doing = 1,
    Review = 2,
    Done = 3,
}

public static class CardStageExtensions
{
    /// <summary>Workflow list the stage lives in (see <c>NexoraStudios.Workflow</c>).</summary>
    public static string ListName(this CardStage stage) => stage.ToString();

    /// <summary>Stage of a card from the workflow list it currently sits in.</summary>
    public static CardStage FromListName(string listName) =>
        Enum.TryParse(listName, out CardStage stage) ? stage : CardStage.Backlog;

    /// <summary>Share of checklist items a team has ticked off by this stage.</summary>
    public static double ChecklistProgress(this CardStage stage) => stage switch
    {
        CardStage.Done => 1.0,
        CardStage.Review => 0.75,
        CardStage.Doing => 0.4,
        _ => 0.0,
    };
}
