using Cardscape.Domain.Workspaces;
using Cardscape.Seeder.Company;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>Plants the demo workspace (single, owned by the
/// first persona) and the per-persona <see cref="WorkspaceMember"/>
/// rows. Memberships are added through the aggregate's
/// <c>AddMember</c> method so the <c>WorkspaceMemberAdded</c>
/// domain event fires and the audit log picks it up.</summary>
public sealed class WorkspacesSeedStep : SeedStepBase
{
    public override string Name => "Workspace + members";
    public override int Order => 20;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        DateTimeOffset now = context.Now;
        if (context.Users.Count == 0)
        {
            Log(log, SeedLogLevel.Warning, "No users to attach to the workspace; skipping.");
            return Task.CompletedTask;
        }

        User owner = context.Users[0];
        context.WorkspaceOwnerId = owner.Id.Value;

        WorkspaceId wsId = WorkspaceId.New();
        WorkspaceName name = WorkspaceName.Create(NexoraStudios.WorkspaceName).Value;
        Result<Workspace> created = Workspace.Create(wsId, name, owner.Id.Value, Region.Unspecified, now);
        if (created.IsFailure)
        {
            Log(log, SeedLogLevel.Error, $"Failed to create workspace: {created.Error.Message}");
            return Task.CompletedTask;
        }

        Workspace workspace = created.Value;
        context.WorkspaceId = wsId;
        context.Db.Workspaces.Add(workspace);
        Log(log, SeedLogLevel.Info, $"  · Workspace '{NexoraStudios.WorkspaceName}' owned by {owner.DisplayName}");

        // Add every other persona as a member with the role
        // declared in their persona definition. The owner is
        // already a member (the Workspace.Create factory
        // adds the owner as the first Admin), so we skip
        // re-adding them.
        for (int i = 1; i < context.Users.Count; i++)
        {
            User member = context.Users[i];
            Persona persona = NexoraStudios.Personas[i - 1] is { } p
                ? p
                : NexoraStudios.Personas[^1];

            WorkspaceRole role = persona.WorkspaceRole switch
            {
                "Admin" => WorkspaceRole.Admin,
                _ => WorkspaceRole.Member
            };

            Result addResult = workspace.AddMember(member.Id.Value, role, now);
            if (addResult.IsFailure)
            {
                Log(log, SeedLogLevel.Warning, $"  ! Could not add {member.DisplayName}: {addResult.Error.Message}");
                continue;
            }

            // The WorkspaceMember was added to the
            // aggregate's navigation. EF Core will persist
            // it via the OwnsMany on Workspace when
            // SaveChanges fires. We only track the
            // reference for the in-memory SeedContext.
            WorkspaceMember? added = workspace.Members.FirstOrDefault(m => m.UserId == member.Id.Value);
            if (added is not null)
            {
                context.WorkspaceMembers.Add(added);
            }
        }

        // Three pending workspace invitations so the
        // "invitations" page has something to show.
        string[] inviteEmails =
        {
            "katherine.johnson@nexora.example",
            "james.maxwell@nexora.example",
            "olga.tokarczuk@nexora.example"
        };
        foreach (string inviteEmail in inviteEmails)
        {
            string plaintext = Generators.PasswordGenerator.RandomUrlSafeToken(24);
            string tokenHash = Generators.PasswordGenerator.Sha256Hex(plaintext);
            string prefix = Generators.PasswordGenerator.Prefix(plaintext, 10);
            Result<WorkspaceInvitation> issued = WorkspaceInvitation.Issue(
                wsId,
                inviteEmail,
                WorkspaceRole.Member,
                owner.Id.Value,
                tokenHash,
                prefix,
                now,
                lifetime: TimeSpan.FromDays(7));
            if (issued.IsSuccess)
            {
                context.Db.WorkspaceInvitations.Add(issued.Value);
                context.WorkspaceInvitations.Add(issued.Value);
            }
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted 1 workspace, {context.WorkspaceMembers.Count + 1} memberships, and {context.WorkspaceInvitations.Count} pending invitations.");
        return Task.CompletedTask;
    }
}
