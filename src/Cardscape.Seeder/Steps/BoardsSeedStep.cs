using Cardscape.Seeder.Company;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;
using Cardscape.Seeder.Simulation;

namespace Cardscape.Seeder.Steps;

/// <summary>
/// Plants every board of the blueprint with its colour, team and stars:
/// the six HQ department boards, last year's archived offsite board and
/// the Labs hackathon board.
/// </summary>
internal sealed class BoardsSeedStep : SeedStepBase
{
    public override string Name => "Boards + members + stars";
    public override int Order => 30;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        SeedTimeline timeline = context.Timeline;
        Workspace hq = context.Workspaces[0];
        Workspace labs = context.Workspaces[1];

        for (int i = 0; i < NexoraStudios.Boards.Count; i++)
        {
            Plant(context, NexoraStudios.Boards[i], hq, timeline.OnDay(-SeedTimeline.HistoryDays + i));
        }

        Board offsite = Plant(context, NexoraStudios.ArchivedBoard, hq, timeline.OnDay(-SeedTimeline.HistoryDays));
        DateTimeOffset archivedAt = timeline.OnDay(-25);
        offsite.Archive(archivedAt);
        context.RecordActivity(offsite, null, context.User(NexoraStudios.ArchivedBoard.OwnerKey), ActivityKind.BoardArchived, archivedAt);

        Plant(context, NexoraStudios.LabsBoard, labs, timeline.OnDay(-28));

        // Everyone stars the board they own; the demo admin also stars the
        // two boards they live in, so the "Starred" home section is full.
        foreach (Board board in context.Boards.Where(b => !b.IsArchived))
        {
            Star(context, board, board.Members.First().UserId);
        }

        Guid ada = context.User(NexoraStudios.Personas[0]).Id.Value;
        Star(context, context.Boards.First(b => b.Name.Value == "Customer Support"), ada);
        Star(context, context.Boards.First(b => b.Name.Value == "Product Discovery"), ada);

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.Boards.Count} boards (1 archived), {context.BoardMembers.Count} board memberships and {context.BoardStars.Count} stars.");
        return Task.CompletedTask;
    }

    private static Board Plant(SeedContext context, BoardBlueprint blueprint, Workspace workspace, DateTimeOffset createdAt)
    {
        User owner = context.User(blueprint.OwnerKey);
        Board board = Board.Create(
            BoardId.New(),
            workspace.Id,
            BoardName.Create(blueprint.Name).Value,
            BoardDescription.Create(blueprint.Description).Value,
            blueprint.Visibility,
            owner.Id.Value,
            createdAt).Value;
        board.ChangeColor(blueprint.Color, createdAt);
        context.Db.Boards.Add(board);
        context.Boards.Add(board);
        context.RecordActivity(board, null, owner, ActivityKind.BoardCreated, createdAt, new { name = blueprint.Name });

        // The first teammate co-administers the board; the rest are members.
        for (int i = 0; i < blueprint.TeamKeys.Count; i++)
        {
            User member = context.User(blueprint.TeamKeys[i]);
            BoardMemberRole role = i == 0 ? BoardMemberRole.Admin : BoardMemberRole.Member;
            board.AddMember(member.Id.Value, role, context.Timeline.Between(createdAt, createdAt.AddDays(3)));
        }

        context.BoardMembers.AddRange(board.Members);
        return board;
    }

    private static void Star(SeedContext context, Board board, Guid userId)
    {
        if (board.IsStarredBy(userId) || board.Star(userId, context.Timeline.Between(board.CreatedAt)).IsFailure)
        {
            return;
        }

        BoardStar star = board.Stars.First(s => s.UserId == userId);
        context.Db.BoardStars.Add(star);
        context.BoardStars.Add(star);
    }
}
