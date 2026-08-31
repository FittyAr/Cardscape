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
    private async Task SaveRecurrenceAsync()
    {
        if (recurrenceIntervalDays < 1) return;
        DateTimeOffset firstOccurrence = DateTimeOffset.UtcNow.AddDays(recurrenceIntervalDays);
        ApiResult<CardRecurrenceDto> result = await Recurrence.SetAsync(
            CardId, recurrenceIntervalDays, firstOccurrence);
        if (result.IsSuccess) recurrence = result.Value;
    }

    private async Task ClearRecurrenceAsync()
    {
        ApiResult result = await Recurrence.DeleteAsync(CardId);
        if (result.IsSuccess) recurrence = null;
    }

    // BETA-8-UI-#17 — see test-results/r8/r8-report.md.
    // Wire Enter on the inline TextBoxes to the same handlers
    // the buttons use, so the user can submit the form without
    // leaving the keyboard.
    private async Task OnCreateChecklistKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (string.Equals(e.Key, "Enter", StringComparison.Ordinal))
        {
            await CreateChecklistAsync();
        }
    }

    private async Task OnAddItemKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e, Guid checklistId)
    {
        if (string.Equals(e.Key, "Enter", StringComparison.Ordinal))
        {
            await AddItemAsync(checklistId);
        }
    }

    private async Task CreateChecklistAsync()
    {
        if (string.IsNullOrWhiteSpace(newChecklistTitle)) return;
        ApiResult<ChecklistDto> result = await Checklists.CreateAsync(CardId, newChecklistTitle);
        if (result.IsSuccess && checklists is not null)
        {
            checklists = [.. checklists, result.Value!];
            newChecklistTitle = string.Empty;
        }
    }

    private async Task AddItemAsync(Guid checklistId)
    {
        if (string.IsNullOrWhiteSpace(newChecklistItemText)) return;
        // BETA-8-API-#3 — backend now returns the freshly-added
        // ChecklistItemDto alone, not the whole checklist. We
        // append the new item to the in-memory list so the UI
        // re-renders without a full GET.
        ApiResult<ChecklistItemDto> result = await Checklists.AddItemAsync(checklistId, newChecklistItemText);
        if (result.IsSuccess && result.Value is not null && checklists is not null)
        {
            checklists = checklists
                .Select(c => c.Id != checklistId
                    ? c
                    : new ChecklistDto(
                        c.Id, c.CardId, c.Title,
                        [.. c.Items, result.Value],
                        CompletedCount: c.CompletedCount,
                        TotalCount: c.TotalCount + 1))
                .ToList();
        }
        newChecklistItemText = string.Empty;
    }

    private async Task ToggleItemAsync(Guid checklistId, Guid itemId)
    {
        ApiResult<ChecklistDto> result = await Checklists.ToggleItemAsync(checklistId, itemId);
        if (result.IsSuccess) await ReplaceChecklistAsync(result.Value!);
    }

    private async Task DeleteItemAsync(Guid checklistId, Guid itemId)
    {
        ApiResult<ChecklistDto> result = await Checklists.DeleteItemAsync(checklistId, itemId);
        if (result.IsSuccess) await ReplaceChecklistAsync(result.Value!);
    }

    private async Task DeleteChecklistAsync(Guid checklistId)
    {
        ApiResult result = await Checklists.DeleteAsync(checklistId);
        if (result.IsSuccess && checklists is not null)
        {
            checklists = checklists.Where(c => c.Id != checklistId).ToList();
        }
    }

    private async Task ReplaceChecklistAsync(ChecklistDto updated)
    {
        if (checklists is null) return;
        checklists = checklists
            .Select(c => c.Id == updated.Id ? updated : c)
            .ToList();
        await Task.CompletedTask;
    }
}

