using Cardscape.Contracts.Settings;
using Cardscape.Domain.Common;

namespace Cardscape.Application.Settings;

/// <summary>The instance quotas from <see cref="LimitSettings"/> as business rules (zero = unlimited).</summary>
public static class LimitSettingsExtensions
{
    public static Result EnsureCanOwnAnotherWorkspace(this LimitSettings limits, int ownedWorkspaces) =>
        Within(limits.MaxWorkspacesPerUser, ownedWorkspaces)
            ? Result.Success()
            : Result.Failure(DomainError.Conflict(
                "workspaces.quota_reached",
                $"You already own the maximum of {limits.MaxWorkspacesPerUser} workspaces allowed on this instance."));

    public static Result EnsureCanAddBoard(this LimitSettings limits, int boardsInWorkspace) =>
        Within(limits.MaxBoardsPerWorkspace, boardsInWorkspace)
            ? Result.Success()
            : Result.Failure(DomainError.Conflict(
                "boards.quota_reached",
                $"This workspace already has the maximum of {limits.MaxBoardsPerWorkspace} boards allowed on this instance."));

    public static TimeSpan InvitationLifetime(this LimitSettings limits) =>
        TimeSpan.FromDays(limits.InvitationLifetimeDays);

    private static bool Within(int limit, int current) => limit <= 0 || current < limit;
}
