using System.Globalization;
using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Cardscape.Web.Shared;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Cardscape.Web.Pages;

// Trello-style "Add to card" side panel: members, labels, dates, cover
// and move, plus editing / deleting your own comments. Board context
// (labels, lists, workspace members) is loaded once per card.
public sealed partial class CardDetail
{
    [Inject] private IListsApiClient ListsApi { get; set; } = default!;
    [Inject] private ILabelsApiClient LabelsApi { get; set; } = default!;
    [Inject] private IBoardsApiClient BoardsApi { get; set; } = default!;
    [Inject] private IWorkspacesApiClient WorkspacesApi { get; set; } = default!;
    [Inject] private TokenStore Tokens { get; set; } = default!;

    private enum CardPanel
    {
        None,
        Members,
        Labels,
        Dates,
        Cover,
        Move,
        Copy,
    }

    private CardPanel _openPanel = CardPanel.None;
    private Guid _boardId;
    private Guid? _currentUserId;
    private IReadOnlyList<LabelDto> _boardLabels = [];
    private IReadOnlyList<BoardListDto> _boardLists = [];
    private IReadOnlyList<WorkspaceMemberDto> _workspaceMembers = [];
    private string _listName = string.Empty;

    // Label composer inside the Labels panel.
    private string _newLabelName = string.Empty;
    private string _newLabelColor = CardPalette.All[0].Hex;

    // Dates panel.
    private DateTime? _dueDateLocal;

    // Move panel.
    private Guid? _moveTargetListId;
    private bool _moveToTop;

    // Copy panel.
    private string _copyTitle = string.Empty;
    private Guid? _copyTargetListId;
    private bool _copyToTop;
    private bool _copying;

    // Comment editing.
    private Guid? _editingCommentId;
    private string _editingCommentBody = string.Empty;

    // The title editor appears on a re-render, where the HTML autofocus
    // attribute does not apply; focus it once it exists.
    private Radzen.Blazor.RadzenTextBox? _titleBox;
    private bool _focusTitle;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusTitle && _titleBox is not null)
        {
            _focusTitle = false;
            await _titleBox.Element.FocusAsync();
        }
    }

    private Guid EffectiveBoardId => BoardId != Guid.Empty ? BoardId : _boardId;

    private IReadOnlyList<LabelDto> CardLabels => _card?.LabelIds is { } ids
        ? _boardLabels.Where(label => ids.Contains(label.Id)).ToList()
        : [];

    private IReadOnlyList<WorkspaceMemberDto> CardMembers => _card?.MemberIds is { } ids
        ? ids.Select(id => _workspaceMembers.FirstOrDefault(member => member.UserId == id))
            .OfType<WorkspaceMemberDto>()
            .ToList()
        : [];

    private bool HasLabel(Guid labelId) => _card?.LabelIds?.Contains(labelId) == true;

    private bool HasMember(Guid userId) => _card?.MemberIds?.Contains(userId) == true;

    private async Task LoadBoardContextAsync()
    {
        if (_card is null)
        {
            return;
        }

        UserSummaryDto? user = await Tokens.GetUserAsync();
        _currentUserId = user?.Id;

        ApiResult<BoardListDto> listResult = await ListsApi.GetAsync(_card.ListId);
        if (!listResult.IsSuccess || listResult.Value is null)
        {
            return;
        }

        _boardId = listResult.Value.BoardId;
        _listName = listResult.Value.Name;

        Task<ApiResult<IReadOnlyList<LabelDto>>> labelsTask = LabelsApi.ListForBoardAsync(_boardId);
        Task<ApiResult<IReadOnlyList<BoardListDto>>> listsTask = ListsApi.ListForBoardAsync(_boardId);
        Task<ApiResult<BoardDto>> boardTask = BoardsApi.GetAsync(_boardId);
        await Task.WhenAll(labelsTask, listsTask, boardTask);

        ApiResult<IReadOnlyList<LabelDto>> labels = await labelsTask;
        _boardLabels = labels.IsSuccess ? labels.Value ?? [] : [];
        ApiResult<IReadOnlyList<BoardListDto>> lists = await listsTask;
        _boardLists = lists.IsSuccess ? (lists.Value ?? []).Where(list => !list.IsArchived).OrderBy(list => list.Position).ToList() : [];

        ApiResult<BoardDto> board = await boardTask;
        if (board.HasValue)
        {
            ApiResult<IReadOnlyList<WorkspaceMemberDto>> members =
                await WorkspacesApi.ListMembersAsync(board.Value.WorkspaceId);
            _workspaceMembers = members.IsSuccess
                ? (members.Value ?? []).OrderBy(member => member.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList()
                : [];
        }
    }

    private void TogglePanel(CardPanel panel)
    {
        _openPanel = _openPanel == panel ? CardPanel.None : panel;
        if (_openPanel == CardPanel.Dates)
        {
            _dueDateLocal = _card?.DueDate?.LocalDateTime ?? DateTime.Today.AddDays(1).AddHours(12);
        }
        else if (_openPanel == CardPanel.Move)
        {
            _moveTargetListId = _card?.ListId;
            _moveToTop = false;
        }
        else if (_openPanel == CardPanel.Copy)
        {
            _copyTitle = _card?.Title ?? string.Empty;
            _copyTargetListId = _card?.ListId;
            _copyToTop = false;
        }
    }

    private string PanelButtonClass(CardPanel panel) =>
        _openPanel == panel ? "cs-aside-button is-active" : "cs-aside-button";

    // Enter / Space activate the div-based picker options (Enter only
    // for text inputs, where Space is a character).
    private static async Task OnOptionKeyAsync(
        Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e, Func<Task> action, bool enterOnly = false)
    {
        if (enterOnly ? e.IsEnter : e.IsActivation)
        {
            await action();
        }
    }

    private void ApplyCard(ApiResult<CardDto> result, string action)
    {
        CaptureCommandOutcome(result, action);
        if (result.HasValue)
        {
            _card = result.Value;
        }
    }

    // ── Members ──────────────────────────────────────────────
    private async Task ToggleMemberAsync(Guid userId)
    {
        ApiResult<CardDto> result = HasMember(userId)
            ? await Cards.UnassignAsync(CardId, userId)
            : await Cards.AssignAsync(CardId, userId);
        ApplyCard(result, L["MembersTitle"]);
    }

    private Task JoinCardAsync() =>
        _currentUserId is { } me ? ToggleMemberAsync(me) : Task.CompletedTask;

    // ── Labels ───────────────────────────────────────────────
    private async Task ToggleLabelAsync(Guid labelId)
    {
        ApiResult<CardDto> result = HasLabel(labelId)
            ? await Cards.DetachLabelAsync(CardId, labelId)
            : await Cards.AttachLabelAsync(CardId, labelId);
        ApplyCard(result, L["CardLabels"]);
    }

    private async Task CreateLabelAsync()
    {
        string name = (_newLabelName ?? string.Empty).Trim();
        if (name.Length == 0 || EffectiveBoardId == Guid.Empty)
        {
            return;
        }

        ApiResult<LabelDto> created = await LabelsApi.CreateAsync(EffectiveBoardId, name, _newLabelColor);
        CaptureCommandOutcome(created, L["CardLabelCreate"]);
        if (created.HasValue)
        {
            _boardLabels = [.. _boardLabels, created.Value];
            _newLabelName = string.Empty;
            await ToggleLabelAsync(created.Value.Id);
        }
    }

    private async Task DeleteLabelAsync(LabelDto label)
    {
        if (!await DialogService.ConfirmDeleteAsync(L, L["CardLabelDeleteConfirm", label.Name], L["CardLabelDelete"]))
        {
            return;
        }

        ApiResult result = await LabelsApi.DeleteAsync(label.Id);
        CaptureCommandOutcome(result, L["CardLabelDelete"]);
        if (result.IsSuccess)
        {
            _boardLabels = _boardLabels.Where(l => l.Id != label.Id).ToList();
            ApiResult<CardDto> refreshed = await Cards.GetAsync(CardId);
            if (refreshed.HasValue)
            {
                _card = refreshed.Value;
            }
        }
    }

    // ── Dates ────────────────────────────────────────────────
    private async Task SaveDueDateAsync()
    {
        if (_dueDateLocal is not { } local)
        {
            return;
        }

        DateTimeOffset due = new(DateTime.SpecifyKind(local, DateTimeKind.Local));
        ApiResult<CardDto> result = await Cards.SetDueDateAsync(CardId, due);
        ApplyCard(result, L["CardDueDate"]);
        if (result.IsSuccess)
        {
            _openPanel = CardPanel.None;
        }
    }

    private async Task ClearDueDateAsync()
    {
        ApiResult<CardDto> result = await Cards.ClearDueDateAsync(CardId);
        ApplyCard(result, L["CardDueDate"]);
        if (result.IsSuccess)
        {
            _openPanel = CardPanel.None;
        }
    }

    private async Task ToggleCompleteFromDueAsync()
    {
        if (_card is null)
        {
            return;
        }

        if (_card.IsCompleted)
        {
            await ReopenAsync();
        }
        else
        {
            await CompleteAsync();
        }
    }

    private string DueStateClass()
    {
        if (_card?.DueDate is not { } due)
        {
            return string.Empty;
        }

        if (_card.IsCompleted)
        {
            return "is-done";
        }

        DateTimeOffset now = DateTimeOffset.Now;
        return due < now ? "is-overdue" : due - now <= TimeSpan.FromHours(48) ? "is-soon" : string.Empty;
    }

    // ── Cover ────────────────────────────────────────────────
    private async Task SetCoverAsync(string? colorName)
    {
        ApiResult<CardDto> result = await Cards.SetCoverAsync(CardId, colorName);
        ApplyCard(result, L["CardCover"]);
    }

    private string? CoverColor => _card?.CoverColor;

    // ── Move ─────────────────────────────────────────────────
    private async Task MoveCardAsync()
    {
        if (_card is null || _moveTargetListId is not { } targetListId)
        {
            return;
        }

        double position = await ComputeEdgePositionAsync(targetListId, _moveToTop, excludeSelf: true);
        ApiResult<CardDto> result = await Cards.MoveAsync(CardId, targetListId, position);
        ApplyCard(result, L["CardMove"]);
        if (result.IsSuccess)
        {
            _listName = _boardLists.FirstOrDefault(list => list.Id == targetListId)?.Name ?? _listName;
            _openPanel = CardPanel.None;
        }
    }

    /// <summary>Position at the top or bottom of a list; a move ignores the card itself, a copy does not.</summary>
    private async Task<double> ComputeEdgePositionAsync(Guid listId, bool top, bool excludeSelf)
    {
        ApiResult<IReadOnlyList<CardSummaryDto>> boardCards =
            await Cards.ListForBoardAsync(EffectiveBoardId, includeArchived: false, includeSnoozed: true);
        List<CardSummaryDto> column = (boardCards.Value ?? [])
            .Where(card => card.ListId == listId && (!excludeSelf || card.Id != CardId))
            .OrderBy(card => card.Position)
            .ToList();
        return top
            ? CardDropPosition.Between(null, column.FirstOrDefault()?.Position)
            : CardDropPosition.Between(column.LastOrDefault()?.Position, null);
    }

    // ── Copy ─────────────────────────────────────────────────
    private async Task CopyCardAsync()
    {
        if (_card is null || _copyTargetListId is not { } targetListId || string.IsNullOrWhiteSpace(_copyTitle))
        {
            return;
        }

        _copying = true;
        try
        {
            double position = await ComputeEdgePositionAsync(targetListId, _copyToTop, excludeSelf: false);
            ApiResult<CardDto> result = await Cards.CopyAsync(CardId, targetListId, _copyTitle.Trim(), position);
            CaptureCommandOutcome(result, L["CardCopy"]);
            if (result.IsSuccess)
            {
                string listName = _boardLists.FirstOrDefault(list => list.Id == targetListId)?.Name ?? _listName;
                Notify.Notify(NotificationSeverity.Success, L["CardCopy"], L["CardCopied", listName]);
                _openPanel = CardPanel.None;
            }
        }
        finally
        {
            _copying = false;
        }
    }

    // ── Comments ─────────────────────────────────────────────
    private bool IsOwnComment(CommentDto comment) =>
        _currentUserId is { } me && comment.AuthorId == me;

    private void StartEditingComment(CommentDto comment)
    {
        _editingCommentId = comment.Id;
        _editingCommentBody = comment.Body;
    }

    private void CancelEditingComment()
    {
        _editingCommentId = null;
        _editingCommentBody = string.Empty;
    }

    private async Task SaveCommentAsync(Guid commentId)
    {
        string body = (_editingCommentBody ?? string.Empty).Trim();
        if (body.Length == 0)
        {
            return;
        }

        ApiResult<CommentDto> result = await Comments.EditAsync(CardId, commentId, body);
        CaptureCommandOutcome(result, L["CommentEdit"]);
        if (result.HasValue && _comments is not null)
        {
            _comments = _comments.Select(c => c.Id == commentId ? result.Value : c).ToList();
            CancelEditingComment();
        }
    }

    private async Task DeleteCommentAsync(Guid commentId)
    {
        if (!await DialogService.ConfirmDeleteAsync(L, L["CommentDeleteConfirm"], L["CommentDelete"]))
        {
            return;
        }

        ApiResult result = await Comments.DeleteAsync(CardId, commentId);
        CaptureCommandOutcome(result, L["CommentDelete"]);
        if (result.IsSuccess && _comments is not null)
        {
            _comments = _comments.Where(c => c.Id != commentId).ToList();
        }
    }

    private static string FormatDue(DateTimeOffset due) =>
        due.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
}
