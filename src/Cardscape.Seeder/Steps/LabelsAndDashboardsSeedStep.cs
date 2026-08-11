using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Dashboards;
using Cardscape.Domain.Labels;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>Plants the four standard labels (Urgent, Bug, Feature,
/// Chore) on every board plus a single dashcard that aggregates
/// overdue cards. Uses the Radzen palette so the labels look at
/// home in the Web UI.</summary>
internal sealed class LabelsAndDashboardsSeedStep : SeedStepBase
{
    private static readonly (string Name, Color Color)[] StandardLabels =
    {
        ("Urgent", Color.Palette.Red),
        ("Bug", Color.Palette.Orange),
        ("Feature", Color.Palette.Green),
        ("Chore", Color.Palette.Gray),
        ("Docs", Color.Palette.Sky),
    };

    public override string Name => "Labels + dashboards";
    public override int Order => 50;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        DateTimeOffset now = context.Now;
        User creator = context.Users[0];

        foreach (Board board in context.Boards)
        {
            int position = 0;
            foreach ((string name, Color color) in StandardLabels)
            {
                Result<Label> created = Label.Create(
                    LabelId.New(),
                    board.Id,
                    LabelName.Create(name).Value,
                    color,
                    creator.Id.Value,
                    now);
                if (created.IsFailure)
                {
                    continue;
                }

                context.Db.Labels.Add(created.Value);
                context.Labels.Add(created.Value);
                position++;
            }

            // Two dashcards per board: overdue count and due-this-week.
            Result<Dashcard> overdue = Dashcard.Create(
                DashcardId.New(),
                board.Id,
                DashcardKind.OverdueCount,
                "Overdue cards",
                "{\"thresholdDays\":0}",
                0,
                creator.Id.Value,
                now);
            if (overdue.IsSuccess)
            {
                context.Add(overdue.Value);
                context.Dashcards.Add(overdue.Value);
            }

            Result<Dashcard> dueThisWeek = Dashcard.Create(
                DashcardId.New(),
                board.Id,
                DashcardKind.DueThisWeek,
                "Due this week",
                "{}",
                1,
                creator.Id.Value,
                now);
            if (dueThisWeek.IsSuccess)
            {
                context.Add(dueThisWeek.Value);
                context.Dashcards.Add(dueThisWeek.Value);
            }
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.Labels.Count} labels and {context.Dashcards.Count} dashcards.");
        return Task.CompletedTask;
    }
}
