using Cardscape.Seeder.Company;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;
using Cardscape.Seeder.Simulation;

namespace Cardscape.Seeder.Steps;

/// <summary>
/// The team talking on its cards: comment threads written by board members
/// after the card existed, @mentions that notify the mentioned teammate, an
/// edited and a deleted comment per board, and votes on boards that have
/// the voting extension.
/// </summary>
internal sealed class ConversationsSeedStep : SeedStepBase
{
    public override string Name => "Conversations: comments, mentions, votes";
    public override int Order => 70;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        SeedTimeline timeline = context.Timeline;
        foreach (Board board in context.Boards)
        {
            IReadOnlyList<User> members = context.MembersOf(board);
            List<Comment> boardComments = [];
            foreach (Card card in context.CardsOf(board))
            {
                int count = timeline.Next(0, 5);
                for (int i = 0; i < count; i++)
                {
                    boardComments.Add(AddComment(context, board, card, members, i));
                }
            }

            ReviseOne(context, board, boardComments);

            if (BoardSetupSeedStep.Blueprint(board).Has(BoardFeatures.Voting))
            {
                PlantVotes(context, board, members);
            }
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.Comments.Count} comments, {context.Notifications.Count(n => n.Kind == NotificationKind.Mentioned)} mentions and {context.CardVotes.Count} votes.");
        return Task.CompletedTask;
    }

    private static Comment AddComment(SeedContext context, Board board, Card card, IReadOnlyList<User> members, int index)
    {
        SeedTimeline timeline = context.Timeline;
        User author = timeline.Pick(members);
        DateTimeOffset at = timeline.Between(card.CreatedAt);

        User? mentioned = timeline.Chance(0.25) ? members.FirstOrDefault(m => m != author && timeline.Chance(0.5)) : null;
        string body = mentioned is null
            ? NexoraStudios.CommentBodies[(index + card.Title.Value.Length) % NexoraStudios.CommentBodies.Count]
            : string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                timeline.Pick(NexoraStudios.MentionBodies),
                mentioned.DisplayName.Value);

        Comment comment = Comment.Create(CommentId.New(), card.Id, author.Id.Value, CommentBody.Create(body).Value, at).Value;
        context.Track(context.Comments, comment);
        context.RecordActivity(board, card.Id.Value, author, ActivityKind.CommentAdded, at, new { commentId = comment.Id.Value });

        if (mentioned is not null)
        {
            context.Notify(Notification.AboutCard(
                mentioned.Id.Value, NotificationKind.Mentioned, card.Id.Value, card.Title.Value, board.Id.Value, author.Id.Value, author.DisplayName.Value, at));
        }

        return comment;
    }

    /// <summary>One comment per board was edited for a typo, another withdrawn by its author.</summary>
    private static void ReviseOne(SeedContext context, Board board, List<Comment> comments)
    {
        if (comments.Count < 2)
        {
            return;
        }

        Comment edited = comments[0];
        User editor = context.Users.First(u => u.Id.Value == edited.AuthorId);
        DateTimeOffset editedAt = context.Timeline.Between(edited.CreatedAt);
        edited.Edit(CommentBody.Create(edited.Body.Value + " (edit: fixed the ticket number)").Value, editor.Id.Value, editedAt);
        context.RecordActivity(board, edited.CardId.Value, editor, ActivityKind.CommentEdited, editedAt, new { commentId = edited.Id.Value });

        Comment withdrawn = comments[^1];
        User author = context.Users.First(u => u.Id.Value == withdrawn.AuthorId);
        DateTimeOffset deletedAt = context.Timeline.Between(withdrawn.CreatedAt);
        withdrawn.Delete(author.Id.Value, deletedAt);
        context.RecordActivity(board, withdrawn.CardId.Value, author, ActivityKind.CommentDeleted, deletedAt, new { commentId = withdrawn.Id.Value });
    }

    private static void PlantVotes(SeedContext context, Board board, IReadOnlyList<User> members)
    {
        foreach (Card card in context.CardsOf(board).Where(c => !c.IsCompleted))
        {
            foreach (User voter in members.Where(_ => context.Timeline.Chance(0.4)))
            {
                CardVote vote = CardVote.Create(CardVoteId.New(), card.Id, voter.Id.Value, context.Timeline.Between(card.CreatedAt)).Value;
                context.Track(context.CardVotes, vote);
            }
        }
    }
}
