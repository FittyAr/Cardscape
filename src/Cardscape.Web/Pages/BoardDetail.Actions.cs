using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Cardscape.Web.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Radzen;

namespace Cardscape.Web.Pages;

public partial class BoardDetail
{
    private async Task ToggleSnoozedAsync()
    {
        if (togglingSnoozed) return;
        togglingSnoozed = true;
        try
        {
            showSnoozed = !showSnoozed;
            await ReloadListsAndCardsAsync();
        }
        finally
        {
            togglingSnoozed = false;
        }
    }

    private async Task ToggleStar()
    {
        if (board is null) return;
        ApiResult<BoardDto> result = board.IsStarred
            ? await BoardsApi.UnstarAsync(BoardId)
            : await BoardsApi.StarAsync(BoardId);
        if (result.IsSuccess)
        {
            board = result.Value;
        }
    }

    // BETA-8-UI-#17 — see test-results/r8/r8-report.md.
    // Enter on the inline TextBox fires KeyDown with key "Enter";
    // we dispatch to the same handler the submit button uses so
    // both paths converge. The @onkeydown attribute is wired in
    // the markup above (AddList form + AddCard inline).
    private async Task OnAddListKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (string.Equals(e.Key, "Enter", StringComparison.Ordinal))
        {
            await AddList();
        }
    }

    private async Task OnAddCardKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e, Guid listId)
    {
        if (string.Equals(e.Key, "Enter", StringComparison.Ordinal))
        {
            await ConfirmAddCard(listId);
        }
    }

    private async Task AddList()
    {
        if (string.IsNullOrWhiteSpace(addListModel.Name)) return;
        addingList = true;
        try
        {
            ApiResult<BoardListDto> result = await ListsApi.CreateAsync(BoardId, addListModel.Name);
            if (result.IsSuccess)
            {
                lists = [.. (lists ?? Array.Empty<BoardListDto>()), result.Value!];
                addListModel.Name = string.Empty;
                showAddList = false;
            }
        }
        finally
        {
            addingList = false;
        }
    }

    // BUG-A4-004 — see test-results/beta/reports/A4-cards-lists.md.
    // The `readonly AddListModel` was initialised once and only
    // cleared on a *successful* submit. If the user opened the
    // form, typed something, and cancelled (or the previous
    // submit failed), the next open would surface the stale
    // value because the same object is bound. Toggle resets the
    // model whenever the form transitions from hidden to visible
    // so the user always starts from a clean textbox.
    private void ToggleAddList()
    {
        if (!showAddList)
        {
            addListModel.Name = string.Empty;
        }
        showAddList = !showAddList;
    }

    private async Task ConfirmAddCard(Guid listId)
    {
        if (string.IsNullOrWhiteSpace(newCardTitle)) return;
        ApiResult<CardDto> result = await CardsApi.CreateAsync(listId, newCardTitle, null);
        if (result.IsSuccess)
        {
            newCardTitle = string.Empty;
            openAddCardFor = null;
            await ReloadListsAndCardsAsync();
        }
    }

    // BUG-A4-002 — per-column context menu handlers. Each calls
    // the corresponding IListsApiClient method (added in this
    // pass) and re-fetches the board so the column reorder /
    // archive state stays in lockstep with the SignalR hub.
    private async Task PromptRenameList(Guid listId, string currentName)
    {
        object? result = await DialogService.OpenAsync<RenameListDialog>(
            "Rename list",
            new Dictionary<string, object?> { { "CurrentName", currentName } },
            new DialogOptions { Width = "420px", Height = "auto", CloseDialogOnOverlayClick = true });

        if (result is string newName && !string.IsNullOrWhiteSpace(newName))
        {
            await RenameList(listId, newName);
        }
    }

    private async Task RenameList(Guid listId, string newName)
    {
        ApiResult<BoardListDto> result = await ListsApi.RenameAsync(listId, newName);
        if (result.IsSuccess)
        {
            await ReloadListsAndCardsAsync();
        }
    }

    private async Task MoveListToPosition(Guid listId, double newPosition)
    {
        ApiResult<BoardListDto> result = await ListsApi.MoveAsync(listId, newPosition);
        if (result.IsSuccess)
        {
            await ReloadListsAndCardsAsync();
        }
    }

    private async Task ArchiveList(Guid listId)
    {
        ApiResult<BoardListDto> result = await ListsApi.ArchiveAsync(listId);
        if (result.IsSuccess)
        {
            await ReloadListsAndCardsAsync();
        }
    }

    private async Task RestoreList(Guid listId)
    {
        ApiResult<BoardListDto> result = await ListsApi.RestoreAsync(listId);
        if (result.IsSuccess)
        {
            await ReloadListsAndCardsAsync();
        }
    }

    // BETA-6-#6 — board settings handlers. All four back the
    // settings panel above; every call already exists on
    // IBoardsApiClient, so this is just glue.
    private async Task RenameBoard()
    {
        if (string.IsNullOrWhiteSpace(renameModel.NewName)) return;
        ApiResult<BoardDto> result = await BoardsApi.RenameAsync(BoardId, renameModel.NewName);
        if (result.IsSuccess)
        {
            board = result.Value;
            renameModel.NewName = string.Empty;
        }
    }

    private async Task ChangeDescription()
    {
        if (string.IsNullOrWhiteSpace(descriptionModel.NewDescription)) return;
        ApiResult<BoardDto> result = await BoardsApi.ChangeDescriptionAsync(
            BoardId, descriptionModel.NewDescription);
        if (result.IsSuccess)
        {
            board = result.Value;
            descriptionModel.NewDescription = string.Empty;
        }
    }

    private async Task ChangeVisibility()
    {
        ApiResult<BoardDto> result = await BoardsApi.ChangeVisibilityAsync(BoardId, newVisibility);
        if (result.IsSuccess)
        {
            board = result.Value;
        }
    }

    private async Task ArchiveBoard()
    {
        ApiResult<BoardDto> result = await BoardsApi.ArchiveAsync(BoardId);
        if (result.IsSuccess) board = result.Value;
    }

    private async Task UnarchiveBoard()
    {
        ApiResult<BoardDto> result = await BoardsApi.UnarchiveAsync(BoardId);
        if (result.IsSuccess) board = result.Value;
    }

    private sealed class RenameBoardModel { public string NewName { get; set; } = string.Empty; }
    private sealed class DescriptionBoardModel { public string NewDescription { get; set; } = string.Empty; }
}

