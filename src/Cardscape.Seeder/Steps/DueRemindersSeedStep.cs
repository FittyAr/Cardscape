using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>
/// The reminders the assignees of open cards would have received: "due
/// soon" for cards due in the next two days and "overdue" for cards whose
/// due date has passed.
/// </summary>
internal sealed class DueRemindersSeedStep : SeedStepBase
{
    private static readonly TimeSpan DueSoonWindow = TimeSpan.FromHours(48);

    public override string Name => "Due-date reminders";
    public override int Order => 80;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        int reminders = 0;
        foreach (Card card in context.Cards.Where(c => c is { IsCompleted: false, IsArchived: false, DueDate: not null }))
        {
            DateTimeOffset due = card.DueDate!.Value;
            (NotificationKind kind, DateTimeOffset at) = due < context.Now
                ? (NotificationKind.Overdue, due.AddHours(1) < context.Now ? due.AddHours(1) : context.Now)
                : (NotificationKind.DueSoon, context.Now.AddHours(-2));
            if (kind == NotificationKind.DueSoon && due - context.Now > DueSoonWindow)
            {
                continue;
            }

            Board board = context.BoardOf(card);
            foreach (CardMember assignee in card.Members)
            {
                context.Notify(Notification.AboutCard(
                    assignee.UserId, kind, card.Id.Value, card.Title.Value, board.Id.Value, actorId: null, actorName: null, at));
                reminders++;
            }
        }

        Log(log, SeedLogLevel.Success, $"Inserted {reminders} due-soon / overdue reminders.");
        return Task.CompletedTask;
    }
}
