using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.UserPreferences;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Users.Commands;
/// <summary>Clears PII on a user row (GDPR Art. 17 final state).
/// The row is kept (so the audit log and the foreign
/// keys from cards / comments / etc. still resolve) but
/// the personal-data fields are replaced with
/// non-personalised placeholders.</summary>
public sealed record AnonymiseUserCommand(Guid UserId);

public static class AnonymiseUserCommandHandler
{
    public static async Task<Result> Handle(
        AnonymiseUserCommand command,
        IUserRepository users,
        IWorkspaceRepository workspaces,
        IUserPreferencesRepository userPreferences,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellation)
    {
        User? user = await users.GetByIdAsync(new UserId(command.UserId), cancellation);
        if (user is null)
        {
            return Result.Failure(DomainError.NotFound(
                "members.user.not_found", "User not found."));
        }

        if (user.IsAnonymised)
        {
            return Result.Success();
        }

        // BETA-7-#4 — see test-results/BETA-TEST-REPORT.md.
        // Anonymisation did not drop the user's workspace /
        // board memberships, so the members list kept
        // showing a placeholder "Anonymised user" with
        // the original role (e.g. admin). Drop every
        // membership the user is not the workspace owner
        // of. Workspace ownership is preserved so the
        // foreign keys from boards still resolve; a
        // follow-up can also reassign the owner.
        IReadOnlyList<Workspace> userWorkspaces = await workspaces.ListForUserAsync(user.Id.Value, cancellation);
        foreach (Workspace ws in userWorkspaces)
        {
            if (ws.OwnerId == user.Id.Value)
            {
                continue;
            }
            ws.RemoveMember(user.Id.Value, clock.UtcNow);
        }

        // v1.2.0 plan §3 commit 2 — drop the preferences
        // row too. The user is being anonymised; the
        // (theme, mode) pair is a per-user preference that
        // no longer has a person behind it.
        await userPreferences.DeleteByUserIdAsync(user.Id.Value, cancellation);

        user.Anonymise(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellation);
        return Result.Success();
    }
}


