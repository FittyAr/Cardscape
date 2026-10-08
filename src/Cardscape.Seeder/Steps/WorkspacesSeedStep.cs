using Cardscape.Seeder.Company;
using Cardscape.Seeder.Generators;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;
using Cardscape.Seeder.Simulation;

namespace Cardscape.Seeder.Steps;

/// <summary>
/// Plants the HQ workspace (every persona, joined over time) and the small
/// Labs workspace, plus one invitation in every lifecycle state: pending,
/// accepted (the contractor), revoked and expired.
/// </summary>
internal sealed class WorkspacesSeedStep : SeedStepBase
{
    public override string Name => "Workspaces + members + invitations";
    public override int Order => 20;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        SeedTimeline timeline = context.Timeline;
        User owner = context.User(NexoraStudios.Personas[0]);
        DateTimeOffset founded = timeline.OnDay(-SeedTimeline.HistoryDays - 5);

        Workspace hq = Create(context, NexoraStudios.WorkspaceName, owner, Region.Europe, founded);
        context.WorkspaceId = hq.Id;
        context.WorkspaceOwnerId = owner.Id.Value;

        // Personas join over the simulated history; the last two joined
        // recently, so their "added to workspace" notifications are unread.
        IReadOnlyList<Persona> joiners = [.. NexoraStudios.Personas.Skip(1)];
        for (int i = 0; i < joiners.Count; i++)
        {
            int dayOffset = i >= joiners.Count - 2
                ? -timeline.Next(1, 3)
                : -SeedTimeline.HistoryDays + (i * 3);
            Join(context, hq, context.User(joiners[i]), joiners[i].WorkspaceRole, timeline.OnDay(dayOffset));
        }

        // The former employee was a member until they left.
        User former = context.User(NexoraStudios.FormerEmployee);
        hq.AddMember(former.Id.Value, WorkspaceRole.Member, founded);
        hq.RemoveMember(former.Id.Value, timeline.OnDay(-35));

        Workspace labs = Create(
            context, NexoraStudios.LabsWorkspaceName, context.User(NexoraStudios.LabsBoard.OwnerKey), Region.Unspecified, timeline.OnDay(-30));
        foreach (string key in NexoraStudios.LabsBoard.TeamKeys)
        {
            Join(context, labs, context.User(key), WorkspaceRole.Member, timeline.OnDay(-29));
        }

        SeedInvitations(context, hq, owner);

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.Workspaces.Count} workspaces, {context.WorkspaceMembers.Count} memberships and {context.WorkspaceInvitations.Count} invitations.");
        return Task.CompletedTask;
    }

    private static Workspace Create(SeedContext context, string name, User owner, Region region, DateTimeOffset at)
    {
        Workspace workspace = Workspace.Create(
            WorkspaceId.New(), WorkspaceName.Create(name).Value, owner.Id.Value, region, at).Value;
        context.Track(context.Workspaces, workspace);
        context.WorkspaceMembers.AddRange(workspace.Members);
        return workspace;
    }

    private static void Join(SeedContext context, Workspace workspace, User user, WorkspaceRole role, DateTimeOffset at)
    {
        if (workspace.AddMember(user.Id.Value, role, at).IsFailure)
        {
            return;
        }

        context.WorkspaceMembers.Add(workspace.Members.First(m => m.UserId == user.Id.Value));
        context.Notify(Notification.AddedToWorkspace(
            user.Id.Value, workspace.Id.Value, workspace.Name.Value, role.ToString(), at));
    }

    private static void SeedInvitations(SeedContext context, Workspace hq, User owner)
    {
        SeedTimeline timeline = context.Timeline;

        Invite(context, hq, owner, "james.maxwell@nexora.example", timeline.OnDay(-2));

        WorkspaceInvitation revoked = Invite(context, hq, owner, "olga.tokarczuk@nexora.example", timeline.OnDay(-9));
        revoked.Revoke(owner.Id.Value, timeline.OnDay(-8));

        // Issued 20 days ago with a 7-day lifetime: expired, never redeemed.
        Invite(context, hq, owner, "alan.turing@nexora.example", timeline.OnDay(-20));

        // The contractor redeemed theirs and is a (restricted) member now.
        User contractor = context.User(NexoraStudios.Contractor);
        WorkspaceInvitation accepted = Invite(context, hq, owner, NexoraStudios.Contractor.Email, timeline.OnDay(-13));
        accepted.Accept(contractor.Id.Value, timeline.OnDay(-12));
        Join(context, hq, contractor, WorkspaceRole.Member, timeline.OnDay(-12));
    }

    private static WorkspaceInvitation Invite(
        SeedContext context, Workspace workspace, User inviter, string email, DateTimeOffset at)
    {
        string token = PasswordGenerator.RandomUrlSafeToken(24);
        WorkspaceInvitation invitation = WorkspaceInvitation.Issue(
            workspace.Id,
            email,
            WorkspaceRole.Member,
            inviter.Id.Value,
            PasswordGenerator.Sha256Hex(token),
            PasswordGenerator.Prefix(token, 10),
            at,
            lifetime: TimeSpan.FromDays(7)).Value;
        context.Track(context.WorkspaceInvitations, invitation);
        return invitation;
    }
}
