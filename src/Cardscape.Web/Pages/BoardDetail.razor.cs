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
    [Inject] private IWorkspacesApiClient WorkspacesApi { get; set; } = default!;

    private IReadOnlyList<KanbanColumn<CardSummaryDto>>? KanbanColumns => _lists?.Select(l =>
        new KanbanColumn<CardSummaryDto>(
            l.Id.ToString(),
            l.Name,
            IsFiltering
                ? _cardsByList.GetValueOrDefault(l.Id, []).Where(MatchesFilter).ToList()
                : _cardsByList.GetValueOrDefault(l.Id, []))
    ).ToList();

    private BoardDto? _board;
    private WorkspaceDto? _workspace;
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
    private IReadOnlyList<VisibilityOption> VisibilityOptions =>
    [
        new("private", L["BoardsVisibilityPrivate"]),
        new("workspace", L["BoardsVisibilityWorkspace"]),
        new("public", L["BoardsVisibilityPublic"])
    ];

    private sealed record VisibilityOption(string Value, string Label);
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
        if (_board is not null)
        {
            ApiResult<WorkspaceDto> wsResult = await WorkspacesApi.GetAsync(_board.WorkspaceId);
            if (wsResult.IsSuccess)
            {
                _workspace = wsResult.Value;
            }
        }

        if (_lastSubscribedBoardId != BoardId)
        {
            // Navigating between boards reuses this component: drop
            // the previous board's transient UI state.
            ClearFilters();
            _showSettings = false;
            _showArchived = false;
            _renamingListId = null;
            _renamingBoard = false;
        }

        await Task.WhenAll(ReloadListsAndCardsAsync(), ReloadAgingModeAsync(), ReloadLabelsAsync());

        if (_lastSubscribedBoardId != BoardId)
        {
            // BETA-8-UI-#4 - see test-results/r8/r8-report.md.
            // Handlers are attached once per component; switching
            // boards only moves the hub group membership, otherwise
            // every event would fire the reload once per board visited.
            Guid previousBoardId = _lastSubscribedBoardId;
            _lastSubscribedBoardId = BoardId;
            await SubscribeToHubAsync(previousBoardId);
        }
    }

    private async Task SubscribeToHubAsync(Guid previousBoardId)
    {
        try
        {
            if (!_subscribedToHub)
            {
                _subscribedToHub = true;
                HubClient.CardCreated += OnHubCardCreatedAsync;
                HubClient.CardUpdated += OnHubCardUpdatedAsync;
                HubClient.CardMoved += OnHubCardMovedAsync;
                HubClient.CardCompleted += OnHubCardCompletedAsync;
                HubClient.CardReopened += OnHubCardReopenedAsync;
                HubClient.CardArchived += OnHubCardArchivedAsync;
                HubClient.CardRestored += OnHubCardRestoredAsync;
                HubClient.ListCreated += OnHubListCreatedAsync;
                HubClient.ListRenamed += OnHubListCreatedAsync;
                HubClient.ListArchived += OnHubListCreatedAsync;
                HubClient.ListRestored += OnHubListCreatedAsync;
                HubClient.CommentAdded += OnHubCommentAddedAsync;
                HubClient.LabelCreated += OnHubLabelCreatedAsync;
            }

            await HubClient.StartAsync();
            if (previousBoardId != Guid.Empty)
            {
                await HubClient.LeaveBoardAsync(previousBoardId);
            }

            await HubClient.JoinBoardAsync(BoardId);
            _hubConnected = HubClient.IsConnected;
        }
        catch
        {
            _hubConnected = false;
        }
    }

    private async Task OnHubCardCreatedAsync(CardEventPayload _)
    {
        await ReloadListsAndCardsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnHubCardUpdatedAsync(CardEventPayload _) =>
        await OnHubCardCreatedAsync(default!);

    private async Task OnHubLabelCreatedAsync(LabelEventPayload _)
    {
        await ReloadLabelsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnHubCardMovedAsync(CardMovedPayload _) =>
        await OnHubCardCreatedAsync(default!);

    private async Task OnHubCardCompletedAsync(CardEventPayload _) =>
        await OnHubCardCreatedAsync(default!);

    private async Task OnHubCardReopenedAsync(CardEventPayload _) =>
        await OnHubCardCreatedAsync(default!);

    private async Task OnHubCardArchivedAsync(CardEventPayload _) =>
        await OnHubCardCreatedAsync(default!);

    private async Task OnHubCardRestoredAsync(CardEventPayload _) =>
        await OnHubCardCreatedAsync(default!);

    private async Task OnHubListCreatedAsync(ListEventPayload _) =>
        await OnHubCardCreatedAsync(default!);

    private async Task OnHubCommentAddedAsync(CommentEventPayload _) =>
        await OnHubCardCreatedAsync(default!);

    public async ValueTask DisposeAsync()
    {
        HubClient.CardCreated -= OnHubCardCreatedAsync;
        HubClient.CardUpdated -= OnHubCardUpdatedAsync;
        HubClient.CardMoved -= OnHubCardMovedAsync;
        HubClient.CardCompleted -= OnHubCardCompletedAsync;
        HubClient.CardReopened -= OnHubCardReopenedAsync;
        HubClient.CardArchived -= OnHubCardArchivedAsync;
        HubClient.CardRestored -= OnHubCardRestoredAsync;
        HubClient.ListCreated -= OnHubListCreatedAsync;
        HubClient.ListRenamed -= OnHubListCreatedAsync;
        HubClient.ListArchived -= OnHubListCreatedAsync;
        HubClient.ListRestored -= OnHubListCreatedAsync;
        HubClient.CommentAdded -= OnHubCommentAddedAsync;
        HubClient.LabelCreated -= OnHubLabelCreatedAsync;

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
        //       ConfirmAddCardAsync, once from OnHubCardCreatedAsync) and the
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
                // Drop positions are computed from the rendered order,
                // so it must be the persisted order.
                next[kv.Key] = kv.Value.OrderBy(card => card.Position).ToList();
            }
        }
        _cardsByList = next;
    }
}
