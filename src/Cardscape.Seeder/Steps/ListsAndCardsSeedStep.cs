using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Seeder.Company;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>Plants four standard lists (Backlog, Doing, Review,
/// Done) on every board, plus a set of cards per list. Card
/// titles come from the per-board catalogue in
/// <see cref="NexoraStudios.CardTitlesByBoard"/>. Every card
/// gets a due date, a label, and a member assignment; a
/// handful get a checklist and / or a recurrence rule.</summary>
public sealed class ListsAndCardsSeedStep : SeedStepBase
{
    private static readonly (string Name, double Position)[] StandardLists =
    {
        ("Backlog", 1.0d),
        ("Doing", 2.0d),
        ("Review", 3.0d),
        ("Done", 4.0d),
    };

    public override string Name => "Lists + cards";
    public override int Order => 60;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        DateTimeOffset now = context.Now;
        var random = new Random(101);
        int totalCards = 0;

        foreach (Board board in context.Boards)
        {
            // 1. The four standard lists.
            Dictionary<string, BoardList> lists = new(StringComparer.OrdinalIgnoreCase);
            foreach ((string listName, double listPosition) in StandardLists)
            {
                BoardListId listId = BoardListId.New();
                Result<BoardList> listResult = BoardList.Create(
                    listId, board.Id, ListName.Create(listName).Value, Position.From(listPosition),
                    context.WorkspaceOwnerId, now);
                if (listResult.IsFailure)
                {
                    continue;
                }

                context.Db.Lists.Add(listResult.Value);
                context.Lists.Add(listResult.Value);
                lists[listName] = listResult.Value;
            }

            // 2. The cards. We pull titles from the
            //    per-board catalogue when one exists, otherwise
            //    fall back to a generic set.
            List<string> titles = NexoraStudios.CardTitlesByBoard.TryGetValue(board.Name.Value, out var t)
                ? t.ToList()
                : NexoraStudios.CardTitlesByBoard["Engineering"].ToList();
            List<Label> boardLabels = context.Labels
                .Where(l => l.BoardId.Value == board.Id.Value)
                .ToList();
            List<User> members = context.Users.Take(Math.Max(1, context.Users.Count / 2)).ToList();

            double position = 1.0d;
            for (int i = 0; i < titles.Count; i++)
            {
                string titleText = titles[i];
                string listName = i switch
                {
                    0 => "Backlog",
                    var n when n == titles.Count - 1 => "Done",
                    var n when n % 5 == 4 => "Review",
                    var n when n % 3 == 2 => "Doing",
                    _ => "Backlog"
                };
                if (!lists.TryGetValue(listName, out BoardList? list))
                {
                    list = lists["Backlog"];
                }

                CardId cardId = CardId.New();
                Result<Card> cardResult = Card.Create(
                    cardId,
                    list.Id,
                    CardTitle.Create(titleText).Value,
                    CardDescription.Create(
                        $"Tracked under the '{board.Name.Value}' board. Owned by " +
                        $"the demo team. {NexoraStudios.CommentBodies[i % NexoraStudios.CommentBodies.Count]}")
                        .Value,
                    Position.From(position),
                    context.WorkspaceOwnerId,
                    now.AddDays(-i));
                if (cardResult.IsFailure)
                {
                    continue;
                }

                Card card = cardResult.Value;

                // 30% of cards are completed (they live in the
                // Done list); the rest get a future due date.
                if (listName == "Done")
                {
                    card.Complete(now.AddDays(-random.Next(1, 10)));
                }
                else
                {
                    card.SetDueDate(now.AddDays(random.Next(1, 21)), now.AddDays(-1));
                }

                // Assign to 1-2 random members.
                User assignee = members[random.Next(0, members.Count)];
                card.Assign(assignee.Id.Value, now);
                if (random.NextDouble() < 0.3 && members.Count > 1)
                {
                    User second = members[random.Next(0, members.Count)];
                    if (second.Id.Value != assignee.Id.Value)
                    {
                        card.Assign(second.Id.Value, now);
                    }
                }

                // Attach one label per card.
                if (boardLabels.Count > 0)
                {
                    Label lbl = boardLabels[random.Next(0, boardLabels.Count)];
                    card.AttachLabel(CardLabel.Create(card.Id, lbl.Id, now), now);
                }

                // 25% of cards get a checklist.
                if (random.NextDouble() < 0.25)
                {
                    Checklist checklist = Checklist.Create(
                        ChecklistId.New(),
                        card.Id,
                        ChecklistTitle.Create("Acceptance criteria").Value,
                        context.WorkspaceOwnerId,
                        now.AddDays(-1)).Value;
                    for (int k = 0; k < 4; k++)
                    {
                        checklist.AddItem(
                            ChecklistItemText.Create($"Step {k + 1}: write / verify the relevant test").Value,
                            Position.From(k + 1),
                            now.AddDays(-1));
                    }
                    context.Db.Checklists.Add(checklist);
                    context.Checklists.Add(checklist);
                    foreach (ChecklistItem item in checklist.Items)
                    {
                        // Items are owned by the checklist
                        // (OwnsMany); EF Core persists them
                        // when the parent is saved.
                        context.ChecklistItems.Add(item);
                    }
                }

                // 15% of cards recur (a fortnight cycle).
                if (random.NextDouble() < 0.15)
                {
                    Result<Cardscape.Domain.Recurrence.CardRecurrence> rec = Cardscape.Domain.Recurrence.CardRecurrence.Create(
                        Cardscape.Domain.Recurrence.CardRecurrenceId.New(),
                        card.Id,
                        14,
                        now.AddDays(14),
                        context.WorkspaceOwnerId,
                        now.AddDays(-1));
                    if (rec.IsSuccess)
                    {
                        context.Db.CardRecurrences.Add(rec.Value);
                        context.CardRecurrences.Add(rec.Value);
                    }
                }

                // 10% of cards are snoozed for the next 48h.
                if (random.NextDouble() < 0.10)
                {
                    Result<CardSnooze> snooze = CardSnooze.Create(
                        card.Id, now.AddHours(48), context.WorkspaceOwnerId, now);
                    if (snooze.IsSuccess)
                    {
                        context.Db.CardSnoozes.Add(snooze.Value);
                        context.CardSnoozes.Add(snooze.Value);
                    }
                }

                context.Db.Cards.Add(card);
                context.Cards.Add(card);
                totalCards++;
                position += 1.0d;
            }
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.Lists.Count} lists and {totalCards} cards.");
        return Task.CompletedTask;
    }
}
