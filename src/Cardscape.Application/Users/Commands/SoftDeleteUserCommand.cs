using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.UserPreferences;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Users.Commands;

/// <summary>
/// Soft-deletes a user (GDPR Art. 17 right-to-erasure
/// grace-period start). The user is hidden from the
/// directory, the auth pipeline rejects sign-in, and a
/// 30-day grace period starts. After the period the
/// retention sweeper hard-deletes the row.
/// </summary>
public sealed record SoftDeleteUserCommand(Guid UserId);

public static class SoftDeleteUserCommandHandler
{
    public static async Task<Result> Handle(
        SoftDeleteUserCommand command,
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

        if (user.IsDeleted)
        {
            return Result.Success();
        }

        // BETA-7-#4 — see test-results/BETA-TEST-REPORT.md.
        // A soft-deleted user kept their workspace + board
        // memberships, so the members list still showed a
        // ghost row. Drop the memberships now so the UI
        // reflects the soft-delete immediately (the
        // retention sweeper would have done it 30 days
        // later, but the soft-delete UI claim is
        // "the user is gone").
        IReadOnlyList<Workspace> userWorkspaces = await workspaces.ListForUserAsync(user.Id.Value, cancellation);
        foreach (Workspace ws in userWorkspaces)
        {
            if (ws.OwnerId == user.Id.Value)
            {
                // The user owns this workspace; skip — a
                // soft-deleted owner is a separate flow
                // (the workspace inherits the
                // anonymised-but-still-resolvable owner).
                continue;
            }
            ws.RemoveMember(user.Id.Value, clock.UtcNow);
        }

        // v1.2.0 plan §3 commit 2 — drop the user's
        // appearance preferences row too. The cookie on
        // the browser may still hold the old theme, but
        // the server-side authority is gone, and the
        // reaper below will hard-delete the user itself
        // after 30 days.
        await userPreferences.DeleteByUserIdAsync(user.Id.Value, cancellation);

        user.SoftDelete(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellation);
        return Result.Success();
    }
}


