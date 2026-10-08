using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Workspaces.Queries;

public sealed record WorkspaceInvitationDto(
    Guid Id,
    Guid WorkspaceId,
    string WorkspaceName,
    string Email,
    WorkspaceRole Role,
    Guid InvitedBy,
    DateTimeOffset InvitedAt,
    DateTimeOffset ExpiresAt,
    string TokenPrefix,
    string? InvitedByName = null,
    Guid? BoardId = null,
    string? BoardName = null);

/// <summary>Names of the boards that "invite to this board" invitations point at.</summary>
internal static class InvitationBoardNames
{
    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IEnumerable<WorkspaceInvitation> invitations, IBoardRepository boards, CancellationToken ct)
    {
        Dictionary<Guid, string> names = [];
        foreach (Guid boardId in invitations.Select(i => i.BoardId).OfType<Guid>().Distinct())
        {
            if (await boards.GetByIdAsync(new BoardId(boardId), ct) is { IsDeleted: false } board)
            {
                names[boardId] = board.Name.Value;
            }
        }

        return names;
    }
}
