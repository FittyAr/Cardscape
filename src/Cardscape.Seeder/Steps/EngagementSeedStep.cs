using Cardscape.Domain.Activities;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Voting;
using Cardscape.Seeder.Company;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>Votes, comments, and activity rows. Pulls a
/// representative slice of every card so the Web UI's card
/// detail page has the full social context (3-5 comments,
/// 1-4 votes, a few activity entries).</summary>
internal sealed class EngagementSeedStep : SeedStepBase
{
    public override string Name => "Votes + comments + activity";
    public override int Order => 70;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        DateTimeOffset now = context.Now;
        var random = new Random(202);
        int totalComments = 0;
        int totalVotes = 0;
        int totalActivities = 0;

        foreach (Card card in context.Cards)
        {
            // 1. 1-4 votes from random users (skip the workspace
            //    owner if they happen to be selected so the count
            //    is varied).
            int voteCount = random.Next(1, 5);
            HashSet<Guid> voted = new();
            for (int v = 0; v < voteCount; v++)
            {
                User voter = context.Users[random.Next(0, context.Users.Count)];
                if (!voted.Add(voter.Id.Value))
                {
                    continue;
                }

                Result<CardVote> vote = CardVote.Create(CardVoteId.New(), card.Id, voter.Id.Value, now.AddDays(-random.Next(1, 14)));
                if (vote.IsSuccess)
                {
                    context.Db.CardVotes.Add(vote.Value);
                    context.CardVotes.Add(vote.Value);
                    totalVotes++;
                }
            }

            // 2. 2-5 comments with bodies from the catalogue.
            int commentCount = random.Next(2, 6);
            for (int c = 0; c < commentCount; c++)
            {
                User author = context.Users[random.Next(0, context.Users.Count)];
                DateTimeOffset when = now.AddDays(-random.Next(0, 14)).AddMinutes(-random.Next(0, 1440));
                // The cast through long keeps Math.Abs
                // safe for the int.MinValue case (where a
                // direct int.Abs would throw).
                int bodyIndex = (int)(Math.Abs((long)card.Id.Value.GetHashCode() + c) % NexoraStudios.CommentBodies.Count);
                string bodyText = NexoraStudios.CommentBodies[bodyIndex];

                Result<Comment> created = Comment.Create(
                    CommentId.New(),
                    card.Id,
                    author.Id.Value,
                    CommentBody.Create(bodyText).Value,
                    when);
                if (created.IsSuccess)
                {
                    context.Db.Comments.Add(created.Value);
                    context.Comments.Add(created.Value);
                    totalComments++;
                }
            }

            // 3. A single activity row for the card creation
            //    event so the board-level activity feed isn't
            //    empty.
            Activity activity = Activity.Create(
                card.CardBoardIdProxy(context),
                card.Id.Value,
                context.WorkspaceOwnerId,
                ActivityKind.CardCreated,
                $"{{\"title\":\"{Escape(card.Title.Value)}\"}}",
                now.AddDays(-7));
            context.Db.Activities.Add(activity);
            context.Activities.Add(activity);
            totalActivities++;
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted {totalVotes} votes, {totalComments} comments, and {totalActivities} activity rows.");
        return Task.CompletedTask;
    }

    private static string Escape(string s) => s.Replace("\"", "\\\"").Replace("\n", " ");
}

internal static class CardEngagementExtensions
{
    /// <summary>Cards don't carry their BoardId directly; we look
    /// it up from the lists seeded in the same run.</summary>
    public static Cardscape.Domain.Boards.BoardId CardBoardIdProxy(this Card card, SeedContext context)
    {
        Cardscape.Domain.Lists.BoardList? list = context.Lists.FirstOrDefault(l => l.Id.Value == card.ListId.Value);
        if (list is null)
        {
            return Cardscape.Domain.Boards.BoardId.New();
        }
        return list.BoardId;
    }
}
