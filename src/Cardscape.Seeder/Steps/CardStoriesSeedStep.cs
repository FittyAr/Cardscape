using Cardscape.Seeder.Company;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;
using Cardscape.Seeder.Simulation;

namespace Cardscape.Seeder.Steps;

/// <summary>
/// Plays back the life of every card: created in the backlog, labelled,
/// assigned, given a due date, then moved through the workflow until the
/// stage it is at today. Every transition writes the same activity row the
/// application handlers write, and recent assignments notify the assignee.
/// Due dates spread from last month to next month (some overdue), so the
/// calendar, planner and due-date dashcards all have something to show.
/// </summary>
internal sealed class CardStoriesSeedStep : SeedStepBase
{
    private static readonly Color[] CoverColors =
        [Color.Palette.Blue, Color.Palette.Green, Color.Palette.Yellow, Color.Palette.Purple, Color.Palette.Sky];

    public override string Name => "Card stories: lifecycle, labels, assignees, due dates";
    public override int Order => 50;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        foreach (Board board in context.Boards)
        {
            new BoardStoryWriter(context, board).WriteAll();
        }

        int overdue = context.Cards.Count(c => c is { IsCompleted: false, IsArchived: false } && c.DueDate < context.Now);
        Log(log, SeedLogLevel.Success,
            $"Inserted {context.Cards.Count} cards ({context.Cards.Count(c => c.IsCompleted)} done, {overdue} overdue, " +
            $"{context.Cards.Count(c => c.IsArchived)} archived) and {context.Activities.Count} activity rows so far.");
        return Task.CompletedTask;
    }

    /// <summary>Writes the stories of one board, keeping per-list positions.</summary>
    private sealed class BoardStoryWriter(SeedContext context, Board board)
    {
        private readonly SeedTimeline _timeline = context.Timeline;
        private readonly IReadOnlyList<User> _members = context.MembersOf(board);
        private readonly IReadOnlyList<Label> _labels = [.. context.Labels.Where(l => l.BoardId == board.Id)];
        private readonly Dictionary<BoardListId, double> _nextPosition = [];

        public void WriteAll()
        {
            IReadOnlyList<string> titles = NexoraStudios.CardTitlesByBoard[board.Name.Value];
            for (int i = 0; i < titles.Count; i++)
            {
                Write(titles[i], i);
            }
        }

        private void Write(string title, int index)
        {
            CardStage stage = board.IsArchived ? CardStage.Done : PickStage(index);
            int doneDay = board.IsArchived ? -_timeline.Next(27, 60) : -_timeline.Next(1, 50);
            DateTimeOffset created = Clamp(_timeline.OnDay(stage switch
            {
                CardStage.Done => doneDay - _timeline.Next(4, 18),
                CardStage.Backlog => -_timeline.Next(3, 60),
                _ => -_timeline.Next(5, 30),
            }));
            DateTimeOffset reached = stage == CardStage.Done
                ? Later(created, _timeline.OnDay(doneDay))
                : _timeline.Between(created);

            User creator = _timeline.Pick(_members);
            Card card = Create(title, index, creator, created);

            DateTimeOffset at = created.AddMinutes(_timeline.Next(5, 120));
            AttachLabels(card, creator, at);
            Assign(card, creator, at);
            SetDueDate(card, stage, doneDay, creator, at.AddMinutes(10));
            Advance(card, stage, created, reached);
            Decorate(card, stage, doneDay, reached);
        }

        private CardStage PickStage(int index) => (index % 10) switch
        {
            0 or 3 or 7 => CardStage.Backlog,
            1 or 5 => CardStage.Doing,
            4 => CardStage.Review,
            _ => _timeline.Chance(0.2) ? CardStage.Review : CardStage.Done,
        };

        private Card Create(string title, int index, User creator, DateTimeOffset at)
        {
            BoardList backlog = context.List(board, CardStage.Backlog.ListName());
            string description =
                $"## Context\n{NexoraStudios.CommentBodies[index % NexoraStudios.CommentBodies.Count]}\n\n" +
                $"## Notes\n- Raised by **{creator.DisplayName.Value}**\n- Tracked on the *{board.Name.Value}* board";
            Card card = Card.Create(
                CardId.New(),
                backlog.Id,
                CardTitle.Create(title).Value,
                CardDescription.Create(description).Value,
                NextPosition(backlog),
                creator.Id.Value,
                at).Value;
            context.Db.Cards.Add(card);
            context.Cards.Add(card);
            Record(card, creator, ActivityKind.CardCreated, at, new { title });
            return card;
        }

        private void AttachLabels(Card card, User actor, DateTimeOffset at)
        {
            foreach (Label label in _labels.OrderBy(_ => _timeline.Next(0, 100)).Take(_timeline.Next(1, 3)))
            {
                card.AttachLabel(CardLabel.Create(card.Id, label.Id, at), at);
                Record(card, actor, ActivityKind.LabelAdded, at, new { labelId = label.Id.Value, name = label.Name.Value });
            }
        }

        private void Assign(Card card, User actor, DateTimeOffset at)
        {
            foreach (User assignee in _members.OrderBy(_ => _timeline.Next(0, 100)).Take(_timeline.Chance(0.3) ? 2 : 1))
            {
                card.Assign(assignee.Id.Value, at);
                Record(card, actor, ActivityKind.CardAssigned, at, new { userId = assignee.Id.Value, userName = assignee.DisplayName.Value });
                if (assignee != actor)
                {
                    context.Notify(Notification.AboutCard(
                        assignee.Id.Value, NotificationKind.AssignedToCard, card.Id.Value, card.Title.Value,
                        board.Id.Value, actor.Id.Value, actor.DisplayName.Value, at));
                }
            }
        }

        private void SetDueDate(Card card, CardStage stage, int doneDay, User actor, DateTimeOffset at)
        {
            int? dueDay = stage switch
            {
                CardStage.Done => doneDay + _timeline.Next(-3, 4),
                CardStage.Review => _timeline.Next(-4, 6),
                CardStage.Doing => _timeline.Next(-6, 15),
                _ => _timeline.Chance(0.55) ? _timeline.Next(4, 50) : null,
            };
            if (dueDay is not int day)
            {
                return;
            }

            DateTimeOffset due = _timeline.DueOn(day);
            card.SetDueDate(due, at);
            Record(card, actor, ActivityKind.CardDueDateSet, at, new { dueDate = due });
        }

        /// <summary>Moves the card list by list until it reaches its stage, at evenly spread times.</summary>
        private void Advance(Card card, CardStage stage, DateTimeOffset created, DateTimeOffset reached)
        {
            int steps = (int)stage;
            User mover = context.Users.First(u => card.Members.Any(m => m.UserId == u.Id.Value));
            for (int step = 1; step <= steps; step++)
            {
                DateTimeOffset at = created + ((reached - created) * step / steps);
                BoardList target = context.List(board, ((CardStage)step).ListName());
                Position position = NextPosition(target);
                card.Move(target.Id, position, at);
                Record(card, mover, ActivityKind.CardMoved, at, new { listId = target.Id.Value, listName = target.Name.Value, position = position.Value });
            }

            if (stage == CardStage.Done)
            {
                card.Complete(reached);
                Record(card, mover, ActivityKind.CardCompleted, reached);
            }
            else if (stage == CardStage.Doing && _timeline.Chance(0.2))
            {
                // Shipped too early: completed, then reopened after QA pushed back.
                DateTimeOffset completed = _timeline.Between(created, reached);
                card.Complete(completed);
                Record(card, mover, ActivityKind.CardCompleted, completed);
                card.Reopen(reached);
                Record(card, mover, ActivityKind.CardReopened, reached);
            }
        }

        private void Decorate(Card card, CardStage stage, int doneDay, DateTimeOffset reached)
        {
            if (_timeline.Chance(0.15))
            {
                card.SetCoverColor(_timeline.Pick(CoverColors), reached);
            }

            if (stage == CardStage.Done && !board.IsArchived && doneDay < -30 && _timeline.Chance(0.4))
            {
                DateTimeOffset archivedAt = Later(reached, reached.AddDays(7));
                card.Archive(archivedAt);
                Record(card, context.User(SetupOwner()), ActivityKind.CardArchived, archivedAt);
            }

            if (stage == CardStage.Backlog && _timeline.Chance(0.12))
            {
                CardSnooze snooze = CardSnooze.Create(
                    card.Id, context.Now.AddDays(_timeline.Next(2, 10)), _members[0].Id.Value, context.Now.AddDays(-1)).Value;
                context.Db.CardSnoozes.Add(snooze);
                context.CardSnoozes.Add(snooze);
            }
        }

        private string SetupOwner() => BoardSetupSeedStep.Blueprint(board).OwnerKey;

        private void Record(Card card, User actor, ActivityKind kind, DateTimeOffset at, object? payload = null) =>
            context.RecordActivity(board, card.Id.Value, actor, kind, at, payload);

        private Position NextPosition(BoardList list)
        {
            double next = _nextPosition.GetValueOrDefault(list.Id, 0) + 1;
            _nextPosition[list.Id] = next;
            return Position.From(next);
        }

        private DateTimeOffset Clamp(DateTimeOffset at) =>
            at > board.CreatedAt ? at : board.CreatedAt.AddHours(_timeline.Next(1, 48));

        /// <summary><paramref name="candidate"/> when it is after <paramref name="after"/>, otherwise a moment later; never past now.</summary>
        private DateTimeOffset Later(DateTimeOffset after, DateTimeOffset candidate)
        {
            DateTimeOffset at = candidate > after ? candidate : after.AddHours(_timeline.Next(2, 30));
            return at < context.Now ? at : context.Now.AddMinutes(-_timeline.Next(5, 60));
        }
    }
}
