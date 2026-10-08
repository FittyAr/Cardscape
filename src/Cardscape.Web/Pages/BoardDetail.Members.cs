using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Cardscape.Web.Shared;
using Radzen;

namespace Cardscape.Web.Pages;

// Board members: the avatar stack on the board bar and the members
// dialog it opens (BoardMembersDialog).
public sealed partial class BoardDetail
{
    private const int MemberAvatarLimit = 3;

    private IReadOnlyList<BoardMemberDto>? _boardMembers;

    private IEnumerable<BoardMemberDto> MemberAvatars =>
        _boardMembers?.Take(MemberAvatarLimit) ?? [];

    private int MemberOverflow =>
        Math.Max(0, (_boardMembers?.Count ?? 0) - MemberAvatarLimit);

    private int MemberCount => _boardMembers?.Count ?? _board?.MemberCount ?? 0;

    private async Task ReloadBoardMembersAsync()
    {
        ApiResult<IReadOnlyList<BoardMemberDto>> result = await BoardsApi.ListMembersAsync(BoardId);
        // A failed load keeps the previous avatars; the button still
        // opens the dialog, which reports the error itself.
        if (result.HasValue)
        {
            _boardMembers = result.Value;
        }
    }

    private async Task OpenMembersAsync()
    {
        if (_board is null)
        {
            return;
        }

        object? result = await DialogService.OpenAsync<BoardMembersDialog>(
            L["BoardMembersTitle"],
            new Dictionary<string, object?>
            {
                { nameof(BoardMembersDialog.BoardId), _board.Id },
                { nameof(BoardMembersDialog.WorkspaceId), _board.WorkspaceId },
                { nameof(BoardMembersDialog.BoardName), _board.Name },
            },
            new DialogOptions { Width = "560px", Height = "auto", CloseDialogOnOverlayClick = true });

        if (result is string outcome && outcome == BoardMembersDialog.Left)
        {
            Nav.NavigateTo($"workspaces/{_board.WorkspaceId}");
            return;
        }

        await ReloadBoardMembersAsync();
    }

    private static string MemberName(BoardMemberDto member) =>
        string.IsNullOrWhiteSpace(member.DisplayName) ? member.Email ?? string.Empty : member.DisplayName;
}
