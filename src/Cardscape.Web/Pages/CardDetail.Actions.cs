using System.Text.Json;
using Cardscape.Web.Resources;
using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Cardscape.Web.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace Cardscape.Web.Pages;

public partial class CardDetail
{
    private async Task ToggleVoteAsync()
    {
        if (togglingVote) return;
        togglingVote = true;
        try
        {
            ApiResult<CardVoteStateDto> result = await Votes.ToggleAsync(CardId);
            if (result.IsSuccess)
            {
                voteState = result.Value;
            }
        }
        finally
        {
            togglingVote = false;
        }
    }

    private async Task Complete()
    {
        if (card is null) return;
        ApiResult<CardDto> result = await Cards.CompleteAsync(CardId);
        if (result.IsSuccess) card = result.Value;
    }

    private async Task Reopen()
    {
        if (card is null) return;
        ApiResult<CardDto> result = await Cards.ReopenAsync(CardId);
        if (result.IsSuccess) card = result.Value;
    }

    private async Task ToggleArchive()
    {
        if (card is null) return;
        ApiResult<CardDto> result = card.IsArchived
            ? await Cards.RestoreAsync(CardId)
            : await Cards.ArchiveAsync(CardId);
        if (result.IsSuccess) card = result.Value;
    }

    // BETA-6-#7 — see test-results/BETA-TEST-REPORT.md.
    // The card DELETE endpoint landed in BETA-5-#5 but the Blazor
    // client never wired a button to it. We add a red "Delete"
    // button in the card actions strip; on success we navigate
    // back to the workspace page (the CardDto does not carry the
    // board id directly, so the cheapest stable post-delete
    // landing is the workspace the user came from — the back
    // button still works for the in-board flow).
    [Inject] private Microsoft.AspNetCore.Components.NavigationManager NavForDelete { get; set; } = default!;

    // BETA-7-#11 — see test-results/BETA-TEST-REPORT.md.
    // The previous incarnation hard-deleted the card on
    // a single click. We gate the action on a
    // `ConfirmAsync` dialog so an accidental click on
    // the wrong button doesn't drop a card.
    private async Task DeleteCard()
    {
        if (card is null)
        {
            return;
        }

        bool confirmed = await DialogService.Confirm(
            $"Delete card \"{card.Title}\"? This cannot be undone.",
            "Delete card",
            new ConfirmOptions
            {
                OkButtonText = "Delete",
                CancelButtonText = "Cancel"
            }) ?? false;
        if (!confirmed)
        {
            return;
        }

        ApiResult result = await Cards.DeleteAsync(CardId);
        if (result.IsSuccess)
        {
            // Send the user back to the workspaces index; the
            // back button or the workspace's board list takes
            // them to the right board without us having to
            // thread the board id through the card DTO.
            NavForDelete.NavigateTo("workspaces");
        }
    }

    private async Task SnoozeAsync()
    {
        if (snoozing || card is null) return;
        if (snoozeUntilLocal <= DateTimeOffset.Now)
        {
            // The backend enforces this too, but failing fast
            // here keeps the user from clicking through a 400.
            return;
        }

        snoozing = true;
        try
        {
            ApiResult<DateTimeOffset> result = await Cards.SnoozeAsync(CardId, snoozeUntilLocal);
            if (result.IsSuccess)
            {
                // Refresh the card so the badge in the header
                // picks up the new snooze without a hard reload.
                await ReloadCardAsync();
            }
        }
        finally
        {
            snoozing = false;
        }
    }

    private async Task UnsnoozeAsync()
    {
        if (snoozing || card is null) return;
        snoozing = true;
        try
        {
            ApiResult result = await Cards.UnsnoozeAsync(CardId);
            if (result.IsSuccess)
            {
                await ReloadCardAsync();
            }
        }
        finally
        {
            snoozing = false;
        }
    }

    // Shared reload for snooze/unsnooze. Keeps the rest of the
    // page (comments, checklists, etc.) intact and only re-pulls
    // the card itself.
    private async Task ReloadCardAsync()
    {
        ApiResult<CardDto> refreshed = await Cards.GetAsync(CardId);
        if (refreshed.IsSuccess && refreshed.Value is not null)
        {
            card = refreshed.Value;
        }
    }

    private async Task AddComment()
    {
        if (string.IsNullOrWhiteSpace(addCommentModel.Body)) return;
        addingComment = true;
        try
        {
            ApiResult<CommentDto> result = await Comments.AddAsync(CardId, addCommentModel.Body);
            if (result.IsSuccess)
            {
                comments = [.. (comments ?? Array.Empty<CommentDto>()), result.Value!];
                addCommentModel.Body = string.Empty;
            }
        }
        finally
        {
            addingComment = false;
        }
    }

    // P3.3 / G6c ” open the "Mirror to..." dialog and, on
    // confirm, call the mirror endpoint. The dialog returns the
    // target list id; the card id is the page's [Parameter].
    private bool mirroring;

    private async Task OpenMirrorDialogAsync()
    {
        if (mirroring) return;

        object? result = await DialogService.OpenAsync<MirrorCardDialog>(
            L["MirrorToTitle"],
            new Dictionary<string, object?> { { "SourceCardId", CardId } },
            new DialogOptions { Width = "460px", Height = "auto", CloseDialogOnOverlayClick = true });

        if (result is not MirrorCardDialog.MirrorCardDialogResult payload)
        {
            return;
        }

        mirroring = true;
        try
        {
            ApiResult<MirrorCardResultDto> mirrorResult =
                await Cards.MirrorToAsync(CardId, payload.TargetListId);
            if (mirrorResult.IsSuccess)
            {
                Notify.Notify(NotificationSeverity.Success, L["MirrorToTitle"],
                    L["MirrorToSuccess"]);
            }
            else
            {
                Notify.Notify(NotificationSeverity.Error, L["MirrorToTitle"],
                    mirrorResult.Error ?? L["MirrorToFailed"]);
            }
        }
        finally
        {
            mirroring = false;
        }
    }

    private sealed class AddCommentModel
    {
        public string Body { get; set; } = string.Empty;
    }
}

