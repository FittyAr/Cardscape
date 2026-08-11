using Cardscape.Domain.Boards;
using Cardscape.Seeder.Company;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>Plants the six department boards in the demo
/// workspace, plus board members and stars. The aggregate
/// factories are used for every row so board creation
/// fires <c>BoardCreated</c> and the audit log picks it up.</summary>
internal sealed class BoardsSeedStep : SeedStepBase
{
    public override string Name => "Boards + members + stars";
    public override int Order => 30;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        DateTimeOffset now = context.Now;
        var random = new Random(7);

        foreach (BoardDefinition def in NexoraStudios.Boards)
        {
            BoardId id = BoardId.New();
            BoardName name = BoardName.Create(def.Name).Value;
            BoardDescription description = BoardDescription.Create(def.Description).Value;
            BoardVisibility visibility = def.Visibility switch
            {
                "Private" => BoardVisibility.Private,
                "Public" => BoardVisibility.Public,
                _ => BoardVisibility.Workspace
            };

            Persona owner = NexoraStudios.Personas[Math.Min(def == NexoraStudios.Boards[0] ? 0 : def == NexoraStudios.Boards[1] ? 3 : def == NexoraStudios.Boards[2] ? 4 : def == NexoraStudios.Boards[3] ? 6 : def == NexoraStudios.Boards[4] ? 8 : 9, NexoraStudios.Personas.Count - 1)];
            User creator = context.Users.First(u => u.Email.Value.StartsWith(owner.EmailLocalPart + "@", StringComparison.Ordinal));

            Result<Board> created = Board.Create(
                id, context.WorkspaceId, name, description, visibility, creator.Id.Value, now);
            if (created.IsFailure)
            {
                Log(log, SeedLogLevel.Error, $"  ! {def.Name}: {created.Error.Message}");
                continue;
            }

            Board board = created.Value;
            context.Db.Boards.Add(board);
            context.Boards.Add(board);
            Log(log, SeedLogLevel.Info, $"  · {def.Name} ({visibility})");

            // The first persona of the matching department
            // (already added as Admin by Board.Create) plus a
            // sprinkling of cross-team members. We use the
            // aggregate's AddMember so the domain event fires.
            int cross = random.Next(2, 5);
            for (int i = 0; i < cross; i++)
            {
                User u = context.Users[random.Next(0, context.Users.Count)];
                if (board.IsMember(u.Id.Value))
                {
                    continue;
                }

                BoardMemberRole role = random.NextDouble() < 0.2
                    ? BoardMemberRole.Admin
                    : BoardMemberRole.Member;
                Result added = board.AddMember(u.Id.Value, role, now);
                if (added.IsFailure)
                {
                    continue;
                }

                BoardMember? member = board.Members.FirstOrDefault(m => m.UserId == u.Id.Value);
                if (member is not null)
                {
                    context.BoardMembers.Add(member);
                }
            }

            // Two stars on every board — the workspace owner
            // and a random member. The aggregate's Star() is
            // idempotent so a double-star is a no-op.
            board.Star(context.WorkspaceOwnerId, now);
            BoardStar? ownerStar = board.Stars.FirstOrDefault(s => s.UserId == context.WorkspaceOwnerId);
            if (ownerStar is not null)
            {
                context.Db.BoardStars.Add(ownerStar);
                context.BoardStars.Add(ownerStar);
            }

            User secondStarrer = context.Users[random.Next(1, context.Users.Count)];
            board.Star(secondStarrer.Id.Value, now);
            BoardStar? secondStar = board.Stars.FirstOrDefault(s => s.UserId == secondStarrer.Id.Value);
            if (secondStar is not null)
            {
                context.Db.BoardStars.Add(secondStar);
                context.BoardStars.Add(secondStar);
            }
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.Boards.Count} boards, {context.BoardMembers.Count} board memberships, and {context.BoardStars.Count} stars.");
        return Task.CompletedTask;
    }
}
