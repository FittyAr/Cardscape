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
    private async Task GenerateDescriptionAsync()
    {
        if (aiBusy)
        {
            return;
        }

        aiBusy = true;
        try
        {
            ApiResult<AiGeneratedTextDto> result = await Ai.GenerateDescriptionAsync(CardId);
            aiGeneratedDescription = result.IsSuccess && result.Value is not null
                ? result.Value.Text
                : null;
        }
        finally
        {
            aiBusy = false;
        }
    }

    private async Task SummarizeCommentsAsync()
    {
        if (aiBusy || comments is null || comments.Count == 0)
        {
            return;
        }

        aiBusy = true;
        try
        {
            IReadOnlyList<Guid> commentIds = comments.Select(c => c.Id).ToList();
            ApiResult<AiGeneratedTextDto> result = await Ai.SummarizeCommentsAsync(commentIds);
            aiSummary = result.IsSuccess && result.Value is not null
                ? result.Value.Text
                : null;
        }
        finally
        {
            aiBusy = false;
        }
    }

    private async Task MakeChecklistAsync()
    {
        if (aiBusy)
        {
            return;
        }

        aiBusy = true;
        try
        {
            ApiResult<AiGeneratedChecklistDto> result = await Ai.GenerateChecklistAsync(CardId);
            if (!result.IsSuccess || result.Value is null || result.Value.Items.Count == 0)
            {
                return;
            }

            ApiResult<ChecklistDto> created = await Checklists.CreateAsync(CardId, "AI suggestions");
            if (!created.IsSuccess || created.Value is null)
            {
                return;
            }

            Guid newChecklistId = created.Value.Id;
            foreach (string item in result.Value.Items)
            {
                // BETA-8-API-#3 — return type is now ChecklistItemDto
                // (we still discard the result here, the next line
                // reloads the checklists to render the final shape).
                await Checklists.AddItemAsync(newChecklistId, item);
            }

            await ReloadChecklistsAsync();
        }
        finally
        {
            aiBusy = false;
        }
    }

    private async Task SuggestOwnersAsync()
    {
        if (aiBusy)
        {
            return;
        }

        aiBusy = true;
        try
        {
            ApiResult<AiOwnerSuggestionsDto> result = await Ai.SuggestOwnersAsync(CardId);
            aiSuggestedOwners = result.IsSuccess && result.Value is not null
                ? result.Value.Suggestions
                : null;
        }
        finally
        {
            aiBusy = false;
        }
    }

    private async Task AssignSuggestedOwnerAsync(AiOwnerSuggestionDto suggestion)
    {
        if (aiBusy || card is null)
        {
            return;
        }

        aiBusy = true;
        try
        {
            ApiResult<CardDto> result = await Cards.AssignAsync(CardId, suggestion.UserId);
            if (result.IsSuccess && result.Value is not null)
            {
                card = result.Value;
            }

            if (aiSuggestedOwners is not null)
            {
                aiSuggestedOwners = aiSuggestedOwners
                    .Where(s => s.UserId != suggestion.UserId)
                    .ToList();
            }
        }
        finally
        {
            aiBusy = false;
        }
    }
}

