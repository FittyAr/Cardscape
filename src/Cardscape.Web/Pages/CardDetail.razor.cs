using System.Globalization;
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
    [Parameter] public Guid CardId { get; set; }

    // BETA-A2-005: the second `@page` template
    // `/cards/{CardId:guid}/{BoardId:guid}` is used by BoardDetail.razor
    // to scope the deep link to the originating board. The parameter
    // is read by `BackToBoardHref` so the trimmer keeps the setter;
    // see BUG-A4-001 — without a reader the IL trimmer silently
    // drops the property from the CardDetail type metadata and Blazor's
    // router then throws "does not have a property matching the name
    // 'BoardId'" the moment a deep link is opened. The getter is the
    // back link the user actually clicks.
    [Parameter] public Guid BoardId { get; set; }

    // BUG-A4-001 — keeps the BoardId property alive through trimming
    // and provides the back-to-board link the deep-link template exists
    // for. Returns the workspaces index when the user opened the card
    // via /cards/{id} (no BoardId in the URL) so the link is always
    // safe to render.
    private string BackToBoardHref =>
        BoardId == Guid.Empty ? "workspaces" : $"boards/{BoardId}";

    private void GoBackToBoard() => Nav.NavigateTo(BackToBoardHref);

    private CardDto? _card;
    private bool _notFound;
    private bool _editingTitle;
    private string _editingTitleValue = string.Empty;
    private CancellationTokenSource? _titleCts;
    private IReadOnlyList<CommentDto>? _comments;
    private string? _commentsError;
    private IReadOnlyList<CustomFieldValueDto>? _fieldValues;
    private string? _fieldValuesError;
    private IReadOnlyList<ActivityDto>? _recentActivity;
    private string? _activityError;
    private CardVoteStateDto? _voteState;
    private string? _voteError;
    private IReadOnlyList<ChecklistDto>? _checklists;
    private string? _checklistsError;
    private string _newChecklistTitle = string.Empty;
    private string _newChecklistItemText = string.Empty;
    private CardRecurrenceDto? _recurrence;
    private string? _recurrenceError;
    private int _recurrenceIntervalDays = 7;
    private bool _addingComment;
    private bool _togglingVote;
    private bool _aiBusy;
    private bool _snoozing;
    private string? _aiGeneratedDescription;
    private string? _aiSummary;
    // BUG-A5-002 — attachments list / upload / download state.
    private IReadOnlyList<AttachmentDto>? _attachments;
    private string? _attachmentsError;
    private bool _uploadingAttachment;
    private IReadOnlyList<AiOwnerSuggestionDto>? _aiSuggestedOwners;
    private readonly AddCommentModel _addCommentModel = new();

    // P3.2 / G6b ” default the snooze picker to "tomorrow 9am"
    // so the common case is one click. The backend rejects
    // values that are not strictly in the future, so the date
    // is computed off the local clock each time the user opens
    // the page (kept in @code so the same default persists
    // across re-renders within the same session). Named
    // `snoozeUntilLocal` to avoid clashing with the
    // `CardDto.SnoozeUntil` property in the markup.
    private DateTimeOffset _snoozeUntilLocal = DateTimeOffset.Now.AddDays(1)
        .Date.AddHours(9);

    private static string CardDateTimeFormat =>
        $"{CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern} HH:mm";

    // P3.4 / MetadataList adapters ” translate the card projection
    // into the IReadOnlyList<MetadataListItem> shape that the
    // <MetadataList> shared component expects. The Members row
    // needs a custom RenderFragment because the AI "Suggest owners"
    // button lives next to the count; the other rows are plain text.
    private IReadOnlyList<MetadataListItem> CardMetaItems => _card is null
        ? Array.Empty<MetadataListItem>()
        : new MetadataListItem[]
        {
            MetadataListItem.Text(L["CardDueDate"],
                _card.DueDate is null
                    ? L["CardNone"]
                    : _card.DueDate.Value.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)),
            new(L["MembersTitle"], MakeMembersValueFragment(_card)),
            MetadataListItem.Text(L["CardLabels"], _card.LabelCount.ToString(CultureInfo.CurrentCulture)),
            // BUG-A5-003 — see test-results/beta/reports/A5-card-extras.md.
            // The header now surfaces comment / attachment /
            // checklist counts alongside the existing member /
            // label counts so the user can see at a glance which
            // cards carry attachments or open discussions.
            MetadataListItem.Text(L["CardComments"], _card.CommentCount.ToString(CultureInfo.CurrentCulture)),
            MetadataListItem.Text(L["CardAttachments"], _card.AttachmentCount.ToString(CultureInfo.CurrentCulture)),
            MetadataListItem.Text(L["CardChecklists"], _card.ChecklistCount.ToString(CultureInfo.CurrentCulture))
        };

    private IReadOnlyList<MetadataListItem> CustomFieldItems => _fieldValues is null
        ? Array.Empty<MetadataListItem>()
        : _fieldValues
            .Select(v => MetadataListItem.Text(FieldKindLabel(v.Kind), FormatFieldValue(v)))
            .ToList();

    private RenderFragment MakeMembersValueFragment(CardDto cardRef) => __builder =>
    {
        __builder.OpenElement(0, "span");
        __builder.AddContent(1, cardRef.MemberCount.ToString(CultureInfo.CurrentCulture));
        __builder.AddContent(2, " ");
        __builder.OpenComponent<Radzen.Blazor.RadzenButton>(3);
        __builder.AddAttribute(4, "Text", $" {L["AiSuggestOwners"]}");
        __builder.AddAttribute(5, "Icon", "auto_awesome");
        __builder.AddAttribute(6, "ButtonStyle", ButtonStyle.Light);
        __builder.AddAttribute(7, "Size", ButtonSize.ExtraSmall);
        __builder.AddAttribute(8, "Click",
            EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(
                this, SuggestOwnersAsync));
        __builder.AddAttribute(9, "Disabled", _aiBusy);
        __builder.AddAttribute(10, "IsBusy", _aiBusy);
        __builder.AddAttribute(11, "Style", "margin-left:.5rem");
        __builder.CloseComponent();
        __builder.CloseElement();
    };

    private async Task ReloadChecklistsAsync()
    {
        ApiResult<IReadOnlyList<ChecklistDto>> checklistsResult =
            await Checklists.ListForCardAsync(CardId);
        _checklists = checklistsResult.IsSuccess ? checklistsResult.Value : null;
        _checklistsError = checklistsResult.IsSuccess
            ? null
            : checklistsResult.Error ?? L["CardSectionLoadFailed", L["CardChecklists"]];
    }

    protected override async Task OnParametersSetAsync()
    {
        ApiResult<CardDto> cardResult = await Cards.GetAsync(CardId);
        if (cardResult.IsSuccess)
        {
            _card = cardResult.Value;
            _notFound = false;
        }
        else
        {
            // Treat both 404 (not found) and 403 (not a member) as
            // "not found" for the page — we do not want to leak the
            // difference to a deep-linked user who has no business
            // knowing the card exists. BETA-8-UI-#5.
            _card = null;
            _notFound = true;
            return;
        }

        Task<ApiResult<IReadOnlyList<CommentDto>>> commentsTask = Comments.ListForCardAsync(CardId);
        Task<ApiResult<IReadOnlyList<CustomFieldValueDto>>> valuesTask = CustomFields.ListValuesForCardAsync(CardId);
        Task<ApiResult<ActivityPageDto>> activityTask = Activities.ListForCardAsync(CardId, cursor: null, limit: 20);
        Task<ApiResult<CardVoteStateDto>> voteTask = Votes.GetStateAsync(CardId);
        Task<ApiResult<IReadOnlyList<ChecklistDto>>> checklistsTask = Checklists.ListForCardAsync(CardId);
        Task<ApiResult<CardRecurrenceDto?>> recurrenceTask = Recurrence.GetAsync(CardId);
        Task<ApiResult<IReadOnlyList<AttachmentDto>>> attachmentsTask = Attachments.ListAsync(CardId);

        await Task.WhenAll(commentsTask, valuesTask, activityTask, voteTask,
            checklistsTask, recurrenceTask, attachmentsTask);

        ApiResult<IReadOnlyList<CommentDto>> commentsResult = await commentsTask;
        (_comments, _commentsError) = CollectionOutcome(commentsResult, L["CardComments"]);
        ApiResult<IReadOnlyList<CustomFieldValueDto>> valuesResult = await valuesTask;
        (_fieldValues, _fieldValuesError) = CollectionOutcome(valuesResult, L["CustomFieldsTitle"]);
        ApiResult<ActivityPageDto> activityResult = await activityTask;
        _recentActivity = activityResult.IsSuccess ? activityResult.Value?.Items ?? [] : null;
        _activityError = ErrorOutcome(activityResult, L["ActivityTitle"]);
        ApiResult<CardVoteStateDto> voteResult = await voteTask;
        _voteState = voteResult.IsSuccess ? voteResult.Value : null;
        _voteError = ErrorOutcome(voteResult, L["CardVotesLabel"]);
        ApiResult<IReadOnlyList<ChecklistDto>> checklistsResult = await checklistsTask;
        (_checklists, _checklistsError) = CollectionOutcome(checklistsResult, L["CardChecklists"]);
        ApiResult<CardRecurrenceDto?> recurrenceResult = await recurrenceTask;
        _recurrence = recurrenceResult.IsSuccess ? recurrenceResult.Value : null;
        _recurrenceError = ErrorOutcome(recurrenceResult, L["CardRecurrence"]);
        ApiResult<IReadOnlyList<AttachmentDto>> attachmentsResult = await attachmentsTask;
        (_attachments, _attachmentsError) = CollectionOutcome(attachmentsResult, L["CardAttachments"]);
    }

    private (IReadOnlyList<T>? Value, string? Error) CollectionOutcome<T>(
        ApiResult<IReadOnlyList<T>> result,
        string section) => result.IsSuccess
            ? (result.Value ?? [], null)
            : (null, result.Error ?? L["CardSectionLoadFailed", section]);

    private string? ErrorOutcome<T>(ApiResult<T> result, string section) =>
        result.IsSuccess ? null : result.Error ?? L["CardSectionLoadFailed", section];

    private void StartEditingTitle()
    {
        if (_card is null)
        {
            return;
        }
        _editingTitleValue = _card.Title;
        _editingTitle = true;
    }

    // BETA-8-UI-#15 - manual description editor. The state lives
    // on the page so a Cancel does not lose the original value
    // until the user clicks Edit again.
    private bool _editingDescription;
    private string _editingDescriptionValue = string.Empty;
    private bool _savingDescription;

    private void StartEditingDescription()
    {
        if (_card is null)
        {
            return;
        }
        _editingDescriptionValue = _card.Description ?? string.Empty;
        _editingDescription = true;
    }

    private async Task SaveDescriptionAsync()
    {
        if (!_editingDescription || _card is null || _savingDescription)
        {
            return;
        }
        _savingDescription = true;
        try
        {
            // BUG-A4-005 — read the value through the form's
            // Data slot instead of the @bind-Value field. The form
            // commits the value on submit, so by the time this
            // handler runs the Data parameter is guaranteed to
            // reflect what the user typed, even if the click
            // happened before the textarea blurred.
            string value = _editingDescriptionValue ?? string.Empty;
            ApiResult<CardDto> result = await Cards.ChangeDescriptionAsync(
                CardId, value);
            if (result.IsSuccess && result.Value is not null)
            {
                _card = result.Value;
            }
            _editingDescription = false;
        }
        finally
        {
            _savingDescription = false;
        }
    }

    private async Task HandleTitleKeyDownAsync(KeyboardEventArgs args)
    {
        if (string.Equals(args.Key, "Enter", StringComparison.Ordinal))
        {
            await SaveTitleAsync();
        }
        else if (string.Equals(args.Key, "Escape", StringComparison.Ordinal))
        {
            _editingTitle = false;
        }
    }

    private async Task SaveTitleAsync()
    {
        if (!_editingTitle || _card is null)
        {
            return;
        }
        string newTitle = (_editingTitleValue ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(newTitle) || newTitle == _card.Title)
        {
            _editingTitle = false;
            return;
        }
        _titleCts?.Cancel();
        _titleCts?.Dispose();
        _titleCts = new CancellationTokenSource();
        ApiResult<CardDto> result = await Cards.RenameAsync(CardId, newTitle, _titleCts.Token);
        if (result.IsSuccess && result.Value is not null)
        {
            _card = result.Value;
        }
        _editingTitle = false;
    }

    public void Dispose()
    {
        _titleCts?.Cancel();
        _titleCts?.Dispose();
        _titleCts = null;
        GC.SuppressFinalize(this);
    }
}
