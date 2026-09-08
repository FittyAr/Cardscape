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

    private IReadOnlyList<KanbanColumn<CardSummaryDto>>? KanbanColumns => _lists?.Select(l =>
        new KanbanColumn<CardSummaryDto>(l.Id.ToString(), l.Name, _cardsByList.GetValueOrDefault(l.Id, []))
    ).ToList();

    private BoardDto? _board;
    private IReadOnlyList<BoardListDto>? _lists;
    private Dictionary<Guid, IReadOnlyList<CardSummaryDto>> _cardsByList = new();
    private bool _showAddList;
    private bool _addingList;
    private readonly AddListModel _addListModel = new();
    private bool _hubConnected;

    // BETA-6-#6 — board settings panel state.
    private bool _showSettings;
    private readonly RenameBoardModel _renameModel = new();
    private readonly DescriptionBoardModel _descriptionModel = new();
    private readonly IReadOnlyList<string> _visibilityOptions = ["private", "workspace", "public"];
    private string _newVisibility = "private";

    private Guid? _openAddCardFor;
    private string _newCardTitle = string.Empty;

    // P3.2 / G6b — "show snoozed" toggle. Default off so the
    // board view matches the API default (snoozed cards are
    // hidden unless the caller asks for them via
    // ?includeSnoozed=true). Flipping the button re-fetches
    // the board cards with the new flag.
    private bool _showSnoozed;
    private bool _togglingSnoozed;

    // Card aging: the board-scoped CardAging extension stores the
    // chosen mode in its ConfigJson. We fetch it once with the rest
    // of the board data and apply the opacity in the card template.
    private const BoardExtensionKind CardAgingKind = BoardExtensionKind.CardAging;
    private CardAgingMode _agingMode = CardAgingMode.Disabled;
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    private Guid _lastSubscribedBoardId;

    protected override async Task OnParametersSetAsync()
    {
        ApiResult<BoardDto> boardResult = await BoardsApi.GetAsync(BoardId);
        _board = boardResult.IsSuccess ? boardResult.Value : null;

        await ReloadListsAndCardsAsync();
        await ReloadAgingModeAsync();

        if (_lastSubscribedBoardId != BoardId)
        {
            // BETA-8-UI-#4 - see test-results/r8/r8-report.md.
            // Reset the hub-subscription guard when the user
            // navigates to a different board; the unsubscribe
            // block in Dispose() handles the previous board.
            _subscribedToHub = false;
            await SubscribeToHubAsync();
            _lastSubscribedBoardId = BoardId;
        }
    }

    private async Task SubscribeToHubAsync()
    {
        if (_subscribedToHub)
        {
            return;
        }
        _subscribedToHub = true;
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
            _hubConnected = HubClient.IsConnected;
        }
        catch
        {
            _hubConnected = false;
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

        GC.SuppressFinalize(this);
    }

    private bool _subscribedToHub;

    private async Task ReloadListsAndCardsAsync()
    {
        ApiResult<IReadOnlyList<BoardListDto>> listsResult = await ListsApi.ListForBoardAsync(BoardId);
        _lists = listsResult.IsSuccess ? listsResult.Value : [];

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
            BoardId, includeArchived: false, includeSnoozed: _showSnoozed);
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
        _cardsByList = next;
    }
}
