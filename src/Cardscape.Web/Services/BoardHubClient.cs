using Cardscape.Web.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cardscape.Web.Services;

/// <summary>
/// Thin SignalR client for the <c>/hubs/board</c> hub. The
/// Blazor page opens a connection, joins the
/// <c>board:{boardId}</c> group, and the server pushes
/// <c>IBoardClient</c> events back.
/// </summary>
public sealed class BoardHubClient : IAsyncDisposable
{
    private readonly TokenStore _tokens;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BoardHubClient> _logger;
    private HubConnection? _connection;

    public BoardHubClient(
        TokenStore tokens,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<BoardHubClient> logger)
    {
        _tokens = tokens;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public event Func<CardEventPayload, Task>? CardCreated;
    public event Func<CardEventPayload, Task>? CardUpdated;
    public event Func<CardMovedPayload, Task>? CardMoved;
    public event Func<CardEventPayload, Task>? CardCompleted;
    public event Func<CardEventPayload, Task>? CardReopened;
    public event Func<CardEventPayload, Task>? CardArchived;
    public event Func<CardEventPayload, Task>? CardRestored;
    public event Func<ListEventPayload, Task>? ListCreated;
    public event Func<ListEventPayload, Task>? ListRenamed;
    public event Func<ListEventPayload, Task>? ListArchived;
    public event Func<ListEventPayload, Task>? ListRestored;
    public event Func<CommentEventPayload, Task>? CommentAdded;
    public event Func<LabelEventPayload, Task>? LabelCreated;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
        {
            return;
        }

        // BETA-6-#2 — see test-results/BETA-TEST-REPORT.md.
        // The previous implementation read `ApiBaseUrl` from
        // config and built `${apiBase}/hubs/board`. When the
        // appsettings.json left the value empty (the same-origin
        // default), `apiBase` was the empty string and the URL
        // became the bare path `/hubs/board`. SignalR's
        // HubConnectionBuilder does NOT resolve that path against
        // the document base like HttpClient does — it goes
        // through the BrowserHttpMessageHandler which inherits
        // the *window* base (often `file:///` for a Blazor WASM
        // document fetched via the API's static-asset path), so
        // the negotiate call ended up at
        // `file:///hubs/board/negotiate?…` and the console threw
        // "Not allowed to load local resource".
        //
        // The fix: use the HttpClient's already-resolved
        // BaseAddress (which Program.cs sets to
        // HostEnvironment.BaseAddress when ApiBaseUrl is empty)
        // so the hub URL is always absolute and points at the
        // same origin as the API calls.
        IHttpClientFactory httpFactory = _httpClientFactory;
        HttpClient http = httpFactory.CreateClient("Cardscape.Api");
        string apiBase = http.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
        if (string.IsNullOrEmpty(apiBase))
        {
            // Fallback for split-origin deploys: respect the
            // explicit ApiBaseUrl setting, just like the typed
            // HttpClient does.
            apiBase = _config["ApiBaseUrl"]?.TrimEnd('/') ?? string.Empty;
        }
        string hubUrl = $"{apiBase}/hubs/board";

        string? accessToken = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(
                "Cannot connect to the board hub: no access token in TokenStore. Sign in first.");
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        Wire(_connection);

        _connection.Closed += error =>
        {
            _logger.LogWarning(error, "Board hub connection closed.");
            return Task.CompletedTask;
        };
        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Board hub reconnected: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };
        _connection.Reconnecting += error =>
        {
            _logger.LogInformation(error, "Board hub reconnecting.");
            return Task.CompletedTask;
        };

        await _connection.StartAsync(ct);
    }

    public async Task JoinBoardAsync(Guid boardId, CancellationToken ct = default)
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("Call StartAsync before JoinBoardAsync.");
        }

        await _connection.InvokeAsync("JoinBoard", boardId, ct);
    }

    public async Task LeaveBoardAsync(Guid boardId, CancellationToken ct = default)
    {
        if (_connection is not null)
        {
            await _connection.InvokeAsync("LeaveBoard", boardId, ct);
        }
    }

    private void Wire(HubConnection connection)
    {
        connection.On<CardEventPayload>("CardCreated", async payload =>
        {
            if (CardCreated is not null)
            {
                await CardCreated.Invoke(payload);
            }
        });
        connection.On<CardEventPayload>("CardUpdated", async payload =>
        {
            if (CardUpdated is not null)
            {
                await CardUpdated.Invoke(payload);
            }
        });
        connection.On<CardMovedPayload>("CardMoved", async payload =>
        {
            if (CardMoved is not null)
            {
                await CardMoved.Invoke(payload);
            }
        });
        connection.On<CardEventPayload>("CardCompleted", async payload =>
        {
            if (CardCompleted is not null)
            {
                await CardCompleted.Invoke(payload);
            }
        });
        connection.On<CardEventPayload>("CardReopened", async payload =>
        {
            if (CardReopened is not null)
            {
                await CardReopened.Invoke(payload);
            }
        });
        connection.On<CardEventPayload>("CardArchived", async payload =>
        {
            if (CardArchived is not null)
            {
                await CardArchived.Invoke(payload);
            }
        });
        connection.On<CardEventPayload>("CardRestored", async payload =>
        {
            if (CardRestored is not null)
            {
                await CardRestored.Invoke(payload);
            }
        });
        connection.On<ListEventPayload>("ListCreated", async payload =>
        {
            if (ListCreated is not null)
            {
                await ListCreated.Invoke(payload);
            }
        });
        connection.On<ListEventPayload>("ListRenamed", async payload =>
        {
            if (ListRenamed is not null)
            {
                await ListRenamed.Invoke(payload);
            }
        });
        connection.On<ListEventPayload>("ListArchived", async payload =>
        {
            if (ListArchived is not null)
            {
                await ListArchived.Invoke(payload);
            }
        });
        connection.On<ListEventPayload>("ListRestored", async payload =>
        {
            if (ListRestored is not null)
            {
                await ListRestored.Invoke(payload);
            }
        });
        connection.On<CommentEventPayload>("CommentAdded", async payload =>
        {
            if (CommentAdded is not null)
            {
                await CommentAdded.Invoke(payload);
            }
        });
        connection.On<LabelEventPayload>("LabelCreated", async payload =>
        {
            if (LabelCreated is not null)
            {
                await LabelCreated.Invoke(payload);
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            try
            {
                await _connection.DisposeAsync();
            }
            catch
            {
                // Best effort.
            }
            _connection = null;
        }
    }
}
