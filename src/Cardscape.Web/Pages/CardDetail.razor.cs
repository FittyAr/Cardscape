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

    private CardDto? card;
    private bool notFound;
    private bool editingTitle;
    private string editingTitleValue = string.Empty;
    private CancellationTokenSource? titleCts;
    private IReadOnlyList<CommentDto>? comments;
    private IReadOnlyList<CustomFieldValueDto>? fieldValues;
    private IReadOnlyList<ActivityDto>? recentActivity;
    private CardVoteStateDto? voteState;
    private IReadOnlyList<ChecklistDto>? checklists;
    private string newChecklistTitle = string.Empty;
    private string newChecklistItemText = string.Empty;
    private CardRecurrenceDto? recurrence;
    private int recurrenceIntervalDays = 7;
    private bool addingComment;
    private bool togglingVote;
    private bool aiBusy;
    private bool snoozing;
    private string? aiGeneratedDescription;
    private string? aiSummary;
    // BUG-A5-002 — attachments list / upload / download state.
    private IReadOnlyList<AttachmentDto>? attachments;
    private bool uploadingAttachment;
    private IReadOnlyList<AiOwnerSuggestionDto>? aiSuggestedOwners;
    private readonly AddCommentModel addCommentModel = new();

    // P3.2 / G6b ” default the snooze picker to "tomorrow 9am"
    // so the common case is one click. The backend rejects
    // values that are not strictly in the future, so the date
    // is computed off the local clock each time the user opens
    // the page (kept in @code so the same default persists
    // across re-renders within the same session). Named
    // `snoozeUntilLocal` to avoid clashing with the
    // `CardDto.SnoozeUntil` property in the markup.
    private DateTimeOffset snoozeUntilLocal = DateTimeOffset.Now.AddDays(1)
        .Date.AddHours(9);

    // P3.4 / MetadataList adapters ” translate the card projection
    // into the IReadOnlyList<MetadataListItem> shape that the
    // <MetadataList> shared component expects. The Members row
    // needs a custom RenderFragment because the AI "Suggest owners"
    // button lives next to the count; the other rows are plain text.
    private IReadOnlyList<MetadataListItem> CardMetaItems => card is null
        ? Array.Empty<MetadataListItem>()
        : new MetadataListItem[]
        {
            MetadataListItem.Text("Due date",
                card.DueDate is null
                    ? "none"
                    : card.DueDate.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)),
            new("Members", MakeMembersValueFragment(card)),
            MetadataListItem.Text("Labels", card.LabelCount.ToString(CultureInfo.CurrentCulture)),
            // BUG-A5-003 — see test-results/beta/reports/A5-card-extras.md.
            // The header now surfaces comment / attachment /
            // checklist counts alongside the existing member /
            // label counts so the user can see at a glance which
            // cards carry attachments or open discussions.
            MetadataListItem.Text("Comments", card.CommentCount.ToString(CultureInfo.CurrentCulture)),
            MetadataListItem.Text("Attachments", card.AttachmentCount.ToString(CultureInfo.CurrentCulture)),
            MetadataListItem.Text("Checklists", card.ChecklistCount.ToString(CultureInfo.CurrentCulture))
        };

    private IReadOnlyList<MetadataListItem> CustomFieldItems => fieldValues is null
        ? Array.Empty<MetadataListItem>()
        : fieldValues
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
        __builder.AddAttribute(9, "Disabled", aiBusy);
        __builder.AddAttribute(10, "IsBusy", aiBusy);
        __builder.AddAttribute(11, "Style", "margin-left:.5rem");
        __builder.CloseComponent();
        __builder.CloseElement();
    };

    private async Task ReloadChecklistsAsync()
    {
        ApiResult<IReadOnlyList<ChecklistDto>> checklistsResult =
            await Checklists.ListForCardAsync(CardId);
        checklists = checklistsResult.IsSuccess ? checklistsResult.Value : [];
        await Task.CompletedTask;
    }

    protected override async Task OnParametersSetAsync()
    {
        ApiResult<CardDto> cardResult = await Cards.GetAsync(CardId);
        if (cardResult.IsSuccess)
        {
            card = cardResult.Value;
            notFound = false;
        }
        else
        {
            // Treat both 404 (not found) and 403 (not a member) as
            // "not found" for the page — we do not want to leak the
            // difference to a deep-linked user who has no business
            // knowing the card exists. BETA-8-UI-#5.
            card = null;
            notFound = true;
        }

        ApiResult<IReadOnlyList<CommentDto>> commentsResult = await Comments.ListForCardAsync(CardId);
        comments = commentsResult.IsSuccess ? commentsResult.Value : [];

        ApiResult<IReadOnlyList<CustomFieldValueDto>> valuesResult =
            await CustomFields.ListValuesForCardAsync(CardId);
        fieldValues = valuesResult.IsSuccess ? valuesResult.Value : [];

        ApiResult<ActivityPageDto> activityResult =
            await Activities.ListForCardAsync(CardId, cursor: null, limit: 20);
        recentActivity = activityResult.IsSuccess ? activityResult.Value?.Items : [];

        ApiResult<CardVoteStateDto> voteResult = await Votes.GetStateAsync(CardId);
        voteState = voteResult.IsSuccess ? voteResult.Value : null;

        ApiResult<IReadOnlyList<ChecklistDto>> checklistsResult = await Checklists.ListForCardAsync(CardId);
        checklists = checklistsResult.IsSuccess ? checklistsResult.Value : [];

        ApiResult<CardRecurrenceDto?> recurrenceResult = await Recurrence.GetAsync(CardId);
        recurrence = recurrenceResult.IsSuccess ? recurrenceResult.Value : null;

        // BUG-A5-002 — fetch the attachments list alongside the
        // rest of the card data so the section is ready when
        // the user scrolls to it.
        ApiResult<IReadOnlyList<AttachmentDto>> attachmentsResult = await Attachments.ListAsync(CardId);
        attachments = attachmentsResult.IsSuccess ? attachmentsResult.Value : [];
    }

    private void StartEditingTitle()
    {
        if (card is null)
        {
            return;
        }
        editingTitleValue = card.Title;
        editingTitle = true;
    }

    // BETA-8-UI-#15 - manual description editor. The state lives
    // on the page so a Cancel does not lose the original value
    // until the user clicks Edit again.
    private bool editingDescription;
    private string editingDescriptionValue = string.Empty;
    private bool savingDescription;

    private void StartEditingDescription()
    {
        if (card is null)
        {
            return;
        }
        editingDescriptionValue = card.Description ?? string.Empty;
        editingDescription = true;
    }

    private async Task SaveDescriptionAsync()
    {
        if (!editingDescription || card is null || savingDescription)
        {
            return;
        }
        savingDescription = true;
        try
        {
            // BUG-A4-005 — read the value through the form's
            // Data slot instead of the @bind-Value field. The form
            // commits the value on submit, so by the time this
            // handler runs the Data parameter is guaranteed to
            // reflect what the user typed, even if the click
            // happened before the textarea blurred.
            string value = editingDescriptionValue ?? string.Empty;
            ApiResult<CardDto> result = await Cards.ChangeDescriptionAsync(
                CardId, value);
            if (result.IsSuccess && result.Value is not null)
            {
                card = result.Value;
            }
            editingDescription = false;
        }
        finally
        {
            savingDescription = false;
        }
    }

    private async Task HandleTitleKeyDown(KeyboardEventArgs args)
    {
        if (string.Equals(args.Key, "Enter", StringComparison.Ordinal))
        {
            await SaveTitleAsync();
        }
        else if (string.Equals(args.Key, "Escape", StringComparison.Ordinal))
        {
            editingTitle = false;
        }
    }

    private async Task SaveTitleAsync()
    {
        if (!editingTitle || card is null)
        {
            return;
        }
        string newTitle = (editingTitleValue ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(newTitle) || newTitle == card.Title)
        {
            editingTitle = false;
            return;
        }
        titleCts?.Cancel();
        titleCts?.Dispose();
        titleCts = new CancellationTokenSource();
        ApiResult<CardDto> result = await Cards.RenameAsync(CardId, newTitle, titleCts.Token);
        if (result.IsSuccess && result.Value is not null)
        {
            card = result.Value;
        }
        editingTitle = false;
    }

    public void Dispose()
    {
        titleCts?.Cancel();
        titleCts?.Dispose();
        titleCts = null;
        GC.SuppressFinalize(this);
    }
}
