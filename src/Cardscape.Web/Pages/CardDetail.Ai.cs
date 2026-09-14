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
        if (_aiBusy)
        {
            return;
        }

        _aiBusy = true;
        try
        {
            ApiResult<AiGeneratedTextDto> result = await Ai.GenerateDescriptionAsync(CardId);
            CaptureAiOutcome(result.IsSuccess && result.Value is not null, result.Error, L["AiGenerateDescription"]);
            _aiGeneratedDescription = result.IsSuccess && result.Value is not null
                ? result.Value.Text
                : null;
        }
        finally
        {
            _aiBusy = false;
        }
    }

    private async Task SummarizeCommentsAsync()
    {
        if (_aiBusy || _comments is null || _comments.Count == 0)
        {
            return;
        }

        _aiBusy = true;
        try
        {
            IReadOnlyList<Guid> commentIds = _comments.Select(c => c.Id).ToList();
            ApiResult<AiGeneratedTextDto> result = await Ai.SummarizeCommentsAsync(commentIds);
            CaptureAiOutcome(result.IsSuccess && result.Value is not null, result.Error, L["AiSummarizeComments"]);
            _aiSummary = result.IsSuccess && result.Value is not null
                ? result.Value.Text
                : null;
        }
        finally
        {
            _aiBusy = false;
        }
    }

    private async Task MakeChecklistAsync()
    {
        if (_aiBusy)
        {
            return;
        }

        _aiBusy = true;
        try
        {
            ApiResult<AiGeneratedChecklistDto> result = await Ai.GenerateChecklistAsync(CardId);
            if (!result.IsSuccess || result.Value is null || result.Value.Items.Count == 0)
            {
                CaptureAiOutcome(false, result.Error, L["AiGenerateChecklist"]);
                return;
            }

            ApiResult<ChecklistDto> created = await Checklists.CreateAsync(CardId, L["CardAiChecklistTitle"]);
            if (!created.IsSuccess || created.Value is null)
            {
                CaptureAiOutcome(false, created.Error, L["AiGenerateChecklist"]);
                return;
            }

            Guid newChecklistId = created.Value.Id;
            foreach (string item in result.Value.Items)
            {
                // BETA-8-API-#3 — return type is now ChecklistItemDto
                // (we still discard the result here, the next line
                // reloads the checklists to render the final shape).
                ApiResult<ChecklistItemDto> added = await Checklists.AddItemAsync(newChecklistId, item);
                if (!added.IsSuccess || added.Value is null)
                {
                    await Checklists.DeleteAsync(newChecklistId);
                    CaptureAiOutcome(false, added.Error, L["AiGenerateChecklist"]);
                    return;
                }
            }

            _commandError = null;
            await ReloadChecklistsAsync();
        }
        finally
        {
            _aiBusy = false;
        }
    }

    private async Task SuggestOwnersAsync()
    {
        if (_aiBusy)
        {
            return;
        }

        _aiBusy = true;
        try
        {
            ApiResult<AiOwnerSuggestionsDto> result = await Ai.SuggestOwnersAsync(CardId);
            CaptureAiOutcome(result.IsSuccess && result.Value is not null, result.Error, L["AiSuggestOwners"]);
            _aiSuggestedOwners = result.IsSuccess && result.Value is not null
                ? result.Value.Suggestions
                : null;
        }
        finally
        {
            _aiBusy = false;
        }
    }

    private async Task AssignSuggestedOwnerAsync(AiOwnerSuggestionDto suggestion)
    {
        if (_aiBusy || _card is null)
        {
            return;
        }

        _aiBusy = true;
        try
        {
            ApiResult<CardDto> result = await Cards.AssignAsync(CardId, suggestion.UserId);
            if (result.IsSuccess && result.Value is not null)
            {
                _card = result.Value;
                _commandError = null;
                if (_aiSuggestedOwners is not null)
                {
                    _aiSuggestedOwners = _aiSuggestedOwners
                        .Where(s => s.UserId != suggestion.UserId)
                        .ToList();
                }
            }
            else
            {
                CaptureAiOutcome(false, result.Error, L["AiSuggestOwners"]);
            }
        }
        finally
        {
            _aiBusy = false;
        }
    }

    private void CaptureAiOutcome(bool succeeded, string? error, string action) =>
        _commandError = succeeded ? null : error ?? L["CardActionFailed", action];
}
