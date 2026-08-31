using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Cardscape.Web.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Radzen;

namespace Cardscape.Web.Pages;

public partial class BoardDetail
{
    [Parameter] public Guid BoardId { get; set; }

    [Inject] private NavigationManager Nav { get; set; } = default!;

    private IReadOnlyList<KanbanColumn<CardSummaryDto>>? KanbanColumns => lists?.Select(l =>
        new KanbanColumn<CardSummaryDto>(l.Id.ToString(), l.Name, cardsByList.GetValueOrDefault(l.Id, []))
    ).ToList();

    private BoardDto? board;
    private IReadOnlyList<BoardListDto>? lists;
    private Dictionary<Guid, IReadOnlyList<CardSummaryDto>> cardsByList = new();
    private bool showAddList;
    private bool addingList;
    private readonly AddListModel addListModel = new();
    private bool hubConnected;

    // BETA-6-#6 — board settings panel state.
    private bool showSettings;
    private readonly RenameBoardModel renameModel = new();
    private readonly DescriptionBoardModel descriptionModel = new();
    private readonly IReadOnlyList<string> visibilityOptions = new[] { "private", "workspace", "public" };
    private string newVisibility = "private";

    private Guid? openAddCardFor;
    private string newCardTitle = string.Empty;

    // P3.2 / G6b — "show snoozed" toggle. Default off so the
    // board view matches the API default (snoozed cards are
    // hidden unless the caller asks for them via
    // ?includeSnoozed=true). Flipping the button re-fetches
    // the board cards with the new flag.
    private bool showSnoozed;
    private bool togglingSnoozed;

    // Card aging: the board-scoped CardAging extension stores the
    // chosen mode in its ConfigJson. We fetch it once with the rest
    // of the board data and apply the opacity in the card template.
    private const BoardExtensionKind CardAgingKind = BoardExtensionKind.CardAging;
    private CardAgingMode agingMode = CardAgingMode.Disabled;
    private DateTimeOffset now = DateTimeOffset.UtcNow;

    private Guid lastSubscribedBoardId;

    protected override async Task OnParametersSetAsync()
    {
        ApiResult<BoardDto> boardResult = await BoardsApi.GetAsync(BoardId);
        board = boardResult.IsSuccess ? boardResult.Value : null;

        await ReloadListsAndCardsAsync();
        await ReloadAgingModeAsync();

        if (lastSubscribedBoardId != BoardId)
        {
            // BETA-8-UI-#4 - see test-results/r8/r8-report.md.
            // Reset the hub-subscription guard when the user
            // navigates to a different board; the unsubscribe
            // block in Dispose() handles the previous board.
            subscribedToHub = false;
            await SubscribeToHubAsync();
            lastSubscribedBoardId = BoardId;
        }
    }

    private async Task SubscribeToHubAsync()
    {
        if (subscribedToHub)
        {
            return;
        }
        subscribedToHub = true;
        try
        {
            HubClient.CardCreated += OnHubCardCreated;
            HubClient.CardMoved += OnHubCardMoved;
            HubClient.CardCompleted += OnHubCardCompleted;
            HubClient.CardReopened += OnHubCardReopened;
            HubClient.CardArchived += OnHubCardArchived;
            HubClient.CardRestored += OnHubCardRestored;
            HubClient.ListCreated += OnHubListCreated;
            HubClient.CommentAdded += OnHubCommentAdded;

            await HubClient.StartAsync();
            await HubClient.JoinBoardAsync(BoardId);
            hubConnected = HubClient.IsConnected;
        }
        catch
        {
            hubConnected = false;
        }
    }

    private async Task OnHubCardCreated(CardEventPayload _)
    {
        await ReloadListsAndCardsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnHubCardMoved(CardMovedPayload _) =>
        await OnHubCardCreated(default!);

    private async Task OnHubCardCompleted(CardEventPayload _) =>
        await OnHubCardCreated(default!);

    private async Task OnHubCardReopened(CardEventPayload _) =>
        await OnHubCardCreated(default!);

    private async Task OnHubCardArchived(CardEventPayload _) =>
        await OnHubCardCreated(default!);

    private async Task OnHubCardRestored(CardEventPayload _) =>
        await OnHubCardCreated(default!);

    private async Task OnHubListCreated(ListEventPayload _) =>
        await OnHubCardCreated(default!);

    private async Task OnHubCommentAdded(CommentEventPayload _) =>
        await OnHubCardCreated(default!);

    public async ValueTask DisposeAsync()
    {
        HubClient.CardCreated -= OnHubCardCreated;
        HubClient.CardMoved -= OnHubCardMoved;
        HubClient.CardCompleted -= OnHubCardCompleted;
        HubClient.CardReopened -= OnHubCardReopened;
        HubClient.CardArchived -= OnHubCardArchived;
        HubClient.CardRestored -= OnHubCardRestored;
        HubClient.ListCreated -= OnHubListCreated;
        HubClient.CommentAdded -= OnHubCommentAdded;

        try
        {
            await HubClient.LeaveBoardAsync(BoardId);
        }
        catch
        {
            // Best effort; connection might already be dead.
        }
    }

    private bool subscribedToHub;

    private async Task ReloadListsAndCardsAsync()
    {
        ApiResult<IReadOnlyList<BoardListDto>> listsResult = await ListsApi.ListForBoardAsync(BoardId);
        lists = listsResult.IsSuccess ? listsResult.Value : [];

        // BETA-7-#12 / BETA-8-UI-#4 - see test-results/BETA-TEST-REPORT.md
        // and test-results/r8/r8-report.md.
        // The previous incarnation had two race conditions:
        //   (a) the SignalR `CardCreated` event fires after the
        //       HTTP 201 returns, so the create-card flow ends up
        //       calling ReloadListsAndCardsAsync twice (once from
        //       ConfirmAddCard, once from OnHubCardCreated) and the
        //       in-place append could duplicate cards if the two
        //       reloads interleaved with Clear() in between;
        //   (b) the hub subscriptions were re-wired on every
        //       OnParametersSetAsync call, so after a hot-reload
        //       the handlers fired N times.
        // Fix: build a brand-new dictionary from the server
        // response (no in-place mutation, no chance of duplicates)
        // and gate the hub subscription so it happens exactly once
        // for the component's lifetime.
        ApiResult<IReadOnlyList<CardSummaryDto>> cardsResult = await CardsApi.ListForBoardAsync(
            BoardId, includeArchived: false, includeSnoozed: showSnoozed);
        Dictionary<Guid, IReadOnlyList<CardSummaryDto>> next = new();
        if (cardsResult.IsSuccess && cardsResult.Value is not null)
        {
            HashSet<Guid> seenIds = [];
            Dictionary<Guid, List<CardSummaryDto>> grouped = new();
            foreach (CardSummaryDto card in cardsResult.Value)
            {
                if (!seenIds.Add(card.Id))
                {
                    continue;
                }

                if (!grouped.TryGetValue(card.ListId, out List<CardSummaryDto>? bucket))
                {
                    bucket = [];
                    grouped[card.ListId] = bucket;
                }

                bucket.Add(card);
            }
            foreach (KeyValuePair<Guid, List<CardSummaryDto>> kv in grouped)
            {
                next[kv.Key] = kv.Value;
            }
        }
        cardsByList = next;
    }
}

