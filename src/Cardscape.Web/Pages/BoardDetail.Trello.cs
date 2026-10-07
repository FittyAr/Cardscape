using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Cardscape.Web.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Radzen;

namespace Cardscape.Web.Pages;

// Trello-parity features of the board page: card filters, archived
// items, inline renames, list drag-to-reorder and quick complete.
public partial class BoardDetail
{
    [Inject] private ILabelsApiClient LabelsApi { get; set; } = default!;

    // ── Filters ──────────────────────────────────────────────
    private enum DueFilter
    {
        NoDate,
        Overdue,
        DueSoon,
        Completed,
        Open,
    }

    private bool _showFilters;
    private string _filterText = string.Empty;
    private readonly HashSet<Guid> _filterLabels = [];
    private readonly HashSet<Guid> _filterMembers = [];
    private readonly HashSet<DueFilter> _filterDue = [];
    private IReadOnlyList<LabelDto> _boardLabels = [];

    private int ActiveFilterCount =>
        (string.IsNullOrWhiteSpace(_filterText) ? 0 : 1) + _filterLabels.Count + _filterMembers.Count + _filterDue.Count;

    private bool IsFiltering => ActiveFilterCount > 0;

    private int VisibleCardCount =>
        _cardsByList.Values.Sum(cards => cards.Count(MatchesFilter));

    private IReadOnlyList<CardSummaryMemberDto> BoardCardMembers =>
        _cardsByList.Values
            .SelectMany(cards => cards)
            .SelectMany(card => card.Members ?? [])
            .GroupBy(member => member.UserId)
            .Select(group => group.First())
            .OrderBy(member => member.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private bool MatchesFilter(CardSummaryDto card)
    {
        if (!IsFiltering)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(_filterText)
            && !card.Title.Contains(_filterText.Trim(), StringComparison.CurrentCultureIgnoreCase)
            && !(card.Labels ?? []).Any(label => label.Name.Contains(_filterText.Trim(), StringComparison.CurrentCultureIgnoreCase)))
        {
            return false;
        }

        if (_filterLabels.Count > 0 && !(card.Labels ?? []).Any(label => _filterLabels.Contains(label.Id)))
        {
            return false;
        }

        if (_filterMembers.Count > 0 && !(card.Members ?? []).Any(member => _filterMembers.Contains(member.UserId)))
        {
            return false;
        }

        return _filterDue.Count == 0 || _filterDue.Any(due => MatchesDue(card, due));
    }

    private bool MatchesDue(CardSummaryDto card, DueFilter filter) => filter switch
    {
        DueFilter.NoDate => card.DueDate is null,
        DueFilter.Overdue => !card.IsCompleted && card.DueDate is { } due && due < _now,
        DueFilter.DueSoon => !card.IsCompleted && card.DueDate is { } due && due >= _now && due - _now <= TimeSpan.FromHours(48),
        DueFilter.Completed => card.IsCompleted,
        DueFilter.Open => !card.IsCompleted,
        _ => true,
    };

    private static void Toggle<T>(HashSet<T> set, T value)
    {
        if (!set.Remove(value))
        {
            set.Add(value);
        }
    }

    private void ClearFilters()
    {
        _filterText = string.Empty;
        _filterLabels.Clear();
        _filterMembers.Clear();
        _filterDue.Clear();
    }

    private string DueFilterLabel(DueFilter filter) => filter switch
    {
        DueFilter.NoDate => L["BoardFilterNoDate"],
        DueFilter.Overdue => L["BoardFilterOverdue"],
        DueFilter.DueSoon => L["BoardFilterDueSoon"],
        DueFilter.Completed => L["BoardFilterCompleted"],
        _ => L["BoardFilterOpen"],
    };

    private async Task ReloadLabelsAsync()
    {
        ApiResult<IReadOnlyList<LabelDto>> result = await LabelsApi.ListForBoardAsync(BoardId);
        _boardLabels = result.IsSuccess ? result.Value ?? [] : [];
    }

    // ── Card front ───────────────────────────────────────────
    // Clicking any label strip toggles names on every card, as in Trello.
    private bool _labelsExpanded;

    private void ToggleLabelNames() => _labelsExpanded = !_labelsExpanded;

    private async Task ToggleCardCompleteAsync(CardSummaryDto card)
    {
        ApiResult<CardDto> result = card.IsCompleted
            ? await CardsApi.ReopenAsync(card.Id)
            : await CardsApi.CompleteAsync(card.Id);
        if (result.IsSuccess)
        {
            await ReloadListsAndCardsAsync();
        }
    }

    private async Task OnCompleteKeyDownAsync(KeyboardEventArgs e, CardSummaryDto card)
    {
        if (e.Key is "Enter" or " ")
        {
            await ToggleCardCompleteAsync(card);
        }
    }

    // ── Archived items ───────────────────────────────────────
    private bool _showArchived;
    private IReadOnlyList<CardSummaryDto> _archivedCards = [];
    private IReadOnlyList<BoardListDto> _archivedLists = [];

    private async Task ToggleArchivedAsync()
    {
        _showArchived = !_showArchived;
        if (_showArchived)
        {
            await ReloadArchivedAsync();
        }
    }

    private async Task ReloadArchivedAsync()
    {
        Task<ApiResult<IReadOnlyList<CardSummaryDto>>> cardsTask =
            CardsApi.ListForBoardAsync(BoardId, includeArchived: true, includeSnoozed: true);
        Task<ApiResult<IReadOnlyList<BoardListDto>>> listsTask =
            ListsApi.ListForBoardAsync(BoardId, includeArchived: true);
        await Task.WhenAll(cardsTask, listsTask);

        ApiResult<IReadOnlyList<CardSummaryDto>> cards = await cardsTask;
        _archivedCards = cards.IsSuccess ? (cards.Value ?? []).Where(card => card.IsArchived).ToList() : [];
        ApiResult<IReadOnlyList<BoardListDto>> lists = await listsTask;
        _archivedLists = lists.IsSuccess ? (lists.Value ?? []).Where(list => list.IsArchived).ToList() : [];
    }

    private async Task RestoreArchivedCardAsync(Guid cardId)
    {
        ApiResult<CardDto> result = await CardsApi.RestoreAsync(cardId);
        if (result.IsSuccess)
        {
            await Task.WhenAll(ReloadArchivedAsync(), ReloadListsAndCardsAsync());
        }
    }

    private async Task DeleteArchivedCardAsync(CardSummaryDto card)
    {
        bool? confirmed = await DialogService.Confirm(
            L["BoardDeleteCardConfirm", card.Title],
            L["ActionDelete"],
            new ConfirmOptions { OkButtonText = L["ActionDelete"], CancelButtonText = L["ActionCancel"] });
        if (confirmed != true)
        {
            return;
        }

        ApiResult result = await CardsApi.DeleteAsync(card.Id);
        if (result.IsSuccess)
        {
            await ReloadArchivedAsync();
        }
    }

    private async Task RestoreArchivedListAsync(Guid listId)
    {
        ApiResult<BoardListDto> result = await ListsApi.RestoreAsync(listId);
        if (result.IsSuccess)
        {
            await Task.WhenAll(ReloadArchivedAsync(), ReloadListsAndCardsAsync());
        }
    }

    private string ArchivedCardListName(CardSummaryDto card) =>
        _lists?.FirstOrDefault(list => list.Id == card.ListId)?.Name
        ?? _archivedLists.FirstOrDefault(list => list.Id == card.ListId)?.Name
        ?? string.Empty;

    // ── Inline renames ───────────────────────────────────────
    private Guid? _renamingListId;
    private string _renamingListName = string.Empty;
    private bool _renamingBoard;
    private string _renamingBoardName = string.Empty;

    // Inline editors appear on a re-render, where the HTML autofocus
    // attribute does not apply; focus them once they exist.
    private Radzen.Blazor.RadzenTextBox? _listRenameBox;
    private Radzen.Blazor.RadzenTextBox? _boardRenameBox;
    private Radzen.Blazor.RadzenTextBox? _cardComposerBox;
    private Radzen.Blazor.RadzenTextBox? _listComposerBox;
    private Func<Radzen.Blazor.RadzenTextBox?>? _focusTarget;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        Radzen.Blazor.RadzenTextBox? box = _focusTarget?.Invoke();
        _focusTarget = null;
        if (box is not null)
        {
            await box.Element.FocusAsync();
        }
    }

    private void StartRenamingList(BoardListDto list)
    {
        _renamingListId = list.Id;
        _renamingListName = list.Name;
        _focusTarget = () => _listRenameBox;
    }

    private async Task CommitListRenameAsync()
    {
        if (_renamingListId is not { } listId)
        {
            return;
        }

        string name = (_renamingListName ?? string.Empty).Trim();
        BoardListDto? current = _lists?.FirstOrDefault(list => list.Id == listId);
        _renamingListId = null;
        if (name.Length > 0 && current is not null && name != current.Name)
        {
            await RenameListAsync(listId, name);
        }
    }

    private async Task OnListRenameKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await CommitListRenameAsync();
        }
        else if (e.Key == "Escape")
        {
            _renamingListId = null;
        }
    }

    private void StartRenamingBoard()
    {
        if (_board is null)
        {
            return;
        }

        _renamingBoardName = _board.Name;
        _renamingBoard = true;
        _focusTarget = () => _boardRenameBox;
    }

    private async Task CommitBoardRenameAsync()
    {
        if (!_renamingBoard || _board is null)
        {
            return;
        }

        _renamingBoard = false;
        string name = (_renamingBoardName ?? string.Empty).Trim();
        if (name.Length == 0 || name == _board.Name)
        {
            return;
        }

        ApiResult<BoardDto> result = await BoardsApi.RenameAsync(BoardId, name);
        if (result.IsSuccess)
        {
            _board = result.Value;
        }
    }

    private async Task OnBoardRenameKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await CommitBoardRenameAsync();
        }
        else if (e.Key == "Escape")
        {
            _renamingBoard = false;
        }
    }

    private void OnBoardTitleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            StartRenamingBoard();
        }
    }

    private void OnListTitleKeyDown(KeyboardEventArgs e, BoardListDto list)
    {
        if (e.Key == "Enter")
        {
            StartRenamingList(list);
        }
    }

    // ── List drag-to-reorder ─────────────────────────────────
    private Guid? _draggingListId;

    private void OnListDragStart(Guid listId)
    {
        _draggingListId = listId;
        _draggingCardId = null;
        _dropTarget = null;
    }

    private async Task<bool> TryDropListAsync(Guid targetListId)
    {
        Guid? draggedId = _draggingListId;
        _draggingListId = null;
        if (draggedId is not { } listId || _lists is null)
        {
            return draggedId is not null;
        }

        List<BoardListDto> ordered = _lists.OrderBy(list => list.Position).ToList();
        int from = ordered.FindIndex(list => list.Id == listId);
        int to = ordered.FindIndex(list => list.Id == targetListId);
        if (from < 0 || to < 0 || from == to)
        {
            return true;
        }

        BoardListDto dragged = ordered[from];
        ordered.RemoveAt(from);
        ordered.Insert(to, dragged);
        double position = CardDropPosition.Between(
            to > 0 ? ordered[to - 1].Position : null,
            to < ordered.Count - 1 ? ordered[to + 1].Position : null);

        await MoveListToPositionAsync(listId, position);
        return true;
    }

    // ── Board settings ───────────────────────────────────────
    private void ToggleSettings()
    {
        _showSettings = !_showSettings;
        if (_showSettings && _board is not null)
        {
            _renameModel.NewName = _board.Name;
            _descriptionModel.NewDescription = _board.Description;
            _newVisibility = _board.Visibility.ToString().ToLowerInvariant();
        }
    }

    // ── Board colour ─────────────────────────────────────────
    private async Task SetBoardColorAsync(string? colorName)
    {
        ApiResult<BoardDto> result = await BoardsApi.SetColorAsync(BoardId, colorName);
        if (result.IsSuccess && result.Value is not null)
        {
            _board = result.Value;
        }
    }

    private static async Task OnSwatchKeyAsync(KeyboardEventArgs e, Func<Task> action)
    {
        if (e.Key is "Enter" or " ")
        {
            await action();
        }
    }

    // ── Copy list ────────────────────────────────────────────
    private async Task PromptCopyListAsync(Guid listId, string currentName)
    {
        object? result = await DialogService.OpenAsync<RenameListDialog>(
            L["ListCopy"],
            new Dictionary<string, object?>
            {
                { "CurrentName", currentName },
                { "Heading", L["ListCopyTitle"].Value },
                { "Blurb", L["ListCopyBlurb"].Value },
                { "ConfirmText", L["ListCopyConfirm"].Value },
            },
            new DialogOptions { Width = "420px", Height = "auto", CloseDialogOnOverlayClick = true });

        if (result is string name && !string.IsNullOrWhiteSpace(name))
        {
            ApiResult<BoardListDto> copy = await ListsApi.CopyAsync(listId, name);
            if (copy.IsSuccess)
            {
                await ReloadListsAndCardsAsync();
            }
        }
    }
}
