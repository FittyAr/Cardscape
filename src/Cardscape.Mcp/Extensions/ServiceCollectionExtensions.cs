using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.DependencyInjection;
using Cardscape.Domain.Security;
using Cardscape.Infrastructure.DependencyInjection;
using Cardscape.Mcp.Authentication;
using Cardscape.Mcp.Authorization;
using Cardscape.Mcp.Idempotency;
using Cardscape.Mcp.Realtime;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Cardscape.Mcp.Extensions;

public static class ServiceCollectionExtensions
{
    public const string McpServerName = "Cardscape";

    /// <summary>
    /// Registers the Model Context Protocol server on top of the
    /// same Application + Infrastructure composition the REST API
    /// uses. Auth is API-token bearer (long-lived tokens minted
    /// by the user via the Web UI's "API tokens" page); tools are
    /// static methods on classes decorated with
    /// <see cref="McpServerToolAttribute"/>.
    /// </summary>
    public static IServiceCollection AddCardscapeMcp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Composition: same as the API ─────────────────────
        services.AddCardscapeApplication();
        services.AddCardscapeInfrastructure(configuration);

        // ── Auth (API-token bearer) ─────────────────────────
        // v0.3 replaces the v0.2 JWT-bearer plumbing with a
        // first-class API-token scheme. The same token works
        // for any AI client; the Web UI mints them via the
        // /api/security/api-tokens endpoints.
        services.AddAuthentication(ApiTokenAuthenticationHandler.SchemeName)
                .AddScheme<ApiTokenAuthenticationOptions,
                           ApiTokenAuthenticationHandler>(
                    ApiTokenAuthenticationHandler.SchemeName,
                    _ => { });

        services.AddAuthorization();
        services.AddHttpContextAccessor();

        // Reuse Application's CurrentUser mapping. MCP owns only the
        // transport adapter that exposes its HttpContext principal.
        services.AddScoped<ICurrentUserAccessor, McpHttpContextCurrentUserAccessor>();

        // ── Real-time (MCP tools that mutate can push to the
        //    same SignalR hub the Web client listens to) ──────
        // The MCP process does not own the hub (the API does), so
        // every successful mutating tool calls the IBoardPushClient
        // which HTTP-calls the API's /api/internal/broadcast
        // webhook. The API dispatches the matching IBoardClient
        // method on the board:{boardId} SignalR group, so a Web
        // client that has joined the board sees the AI's edit in
        // real time.
        string? apiBaseUrl = configuration["Cardscape:ApiBaseUrl"]
            ?? configuration["ApiBaseUrl"]
            ?? Environment.GetEnvironmentVariable("CARDS_CAPE__APIBASEURL")
            ?? "http://localhost:5291/";
        services.AddHttpClient("Cardscape.Api", client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<IBoardPushClient, HttpBoardPushClient>();

        // MCP subscriptions: per-board fan-out of ResourceUpdated
        // notifications. The broadcaster is process-wide; the
        // Web UI's SignalR hub pushes every board change to
        // BroadcastAsync through the internal broadcast endpoint.
        services.AddSingleton<McpResourceBroadcaster>();

        // ── MCP server (stdio transport) ─────────────────────
        services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
            .WithResourcesFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
            .WithPromptsFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
            .WithRequestFilters(filters =>
            {
                filters.AddCallToolFilter(next => async (request, cancellationToken) =>
                {
                    ICurrentUserAccessor accessor = request.Services!
                        .GetRequiredService<ICurrentUserAccessor>();
                    return await McpToolScopePolicy.AuthorizeAndInvokeAsync(
                        request.Params?.Name,
                        accessor.GetCurrentPrincipal(),
                        async () =>
                        {
                            IServiceProvider requestServices = request.Services!;
                            return await McpToolIdempotencyPolicy.InvokeAsync(
                                request.Params?.Name,
                                request.Params?.Arguments,
                                request.Params?.Meta,
                                requestServices.GetRequiredService<ICurrentUser>(),
                                requestServices.GetRequiredService<IIdempotencyKeyStore>(),
                                requestServices.GetRequiredService<IClock>(),
                                () => next(request, cancellationToken),
                                cancellationToken);
                        });
                });
                filters.AddListResourceTemplatesFilter(RequireReadScope);
                filters.AddListResourcesFilter(RequireReadScope);
                filters.AddReadResourceFilter(RequireReadScope);
                filters.AddListPromptsFilter(RequireReadScope);
                filters.AddGetPromptFilter(RequireReadScope);
                filters.AddCompleteFilter(RequireReadScope);
                filters.AddSubscribeToResourcesFilter(RequireReadScope);
                filters.AddUnsubscribeFromResourcesFilter(RequireReadScope);
            })
            // The .NET MCP SDK 1.4+ routes the
            // resources/subscribe and resources/unsubscribe
            // requests through the request handler pipeline.
            // The handlers below delegate to
            // McpResourceBroadcaster, which keeps a per-URI
            // list of subscribed McpServer instances and uses
            // them to fan out notifications/resources/updated
            // when BroadcastAsync is called.
            .WithSubscribeToResourcesHandler(SubscribeToResourceAsync)
            .WithUnsubscribeFromResourcesHandler(UnsubscribeFromResourceAsync);

        return services;
    }

    private static McpRequestHandler<TParams, TResult> RequireReadScope<TParams, TResult>(
        McpRequestHandler<TParams, TResult> next) =>
        async (request, cancellationToken) =>
        {
            ICurrentUserAccessor accessor = request.Services!
                .GetRequiredService<ICurrentUserAccessor>();
            return await McpScopeAuthorization.AuthorizeAndInvokeAsync(
                Scope.Read,
                typeof(TParams).Name,
                accessor.GetCurrentPrincipal(),
                () => next(request, cancellationToken));
        };

    private static async ValueTask<EmptyResult> SubscribeToResourceAsync(
        RequestContext<SubscribeRequestParams> request,
        CancellationToken cancellationToken)
    {
        SubscribeRequestParams? parameters = request.Params;
        if (parameters is null || string.IsNullOrWhiteSpace(parameters.Uri))
        {
            throw new ArgumentException(
                "subscribe request is missing a uri parameter.", nameof(request));
        }

        IServiceProvider services = request.Services!;
        ICurrentUser currentUser = services.GetRequiredService<ICurrentUser>();
        if (currentUser.Id is null)
        {
            throw new ModelContextProtocol.McpException(
                $"{McpBoardSubscriptionAuthorization.ForbiddenErrorCode}: An authenticated user is required.");
        }

        Guid userId = currentUser.Id.Value;
        IBoardRepository boards = services.GetRequiredService<IBoardRepository>();
        string canonicalUri = await McpBoardSubscriptionAuthorization.AuthorizeAsync(
            parameters.Uri,
            userId,
            boards,
            cancellationToken);

        McpResourceBroadcaster broadcaster = services
            .GetRequiredService<McpResourceBroadcaster>();
        broadcaster.Subscribe(canonicalUri, request.Server, userId);
        return new EmptyResult();
    }

    private static ValueTask<EmptyResult> UnsubscribeFromResourceAsync(
        RequestContext<UnsubscribeRequestParams> request,
        CancellationToken cancellationToken)
    {
        UnsubscribeRequestParams? parameters = request.Params;
        if (parameters is null || string.IsNullOrWhiteSpace(parameters.Uri))
        {
            throw new ArgumentException(
                "unsubscribe request is missing a uri parameter.", nameof(request));
        }

        string canonicalUri = McpBoardSubscriptionAuthorization.ToCanonicalUri(
            McpBoardSubscriptionAuthorization.ParseBoardId(parameters.Uri));
        McpResourceBroadcaster broadcaster = request.Services!
            .GetRequiredService<McpResourceBroadcaster>();
        broadcaster.Unsubscribe(canonicalUri, request.Server);
        return ValueTask.FromResult(new EmptyResult());
    }

    /// <summary>
    /// Maps the MCP server pipeline. Stdio doesn't need HTTP
    /// endpoints, but we still wire auth + authorization so tools
    /// that reach into <see cref="ICurrentUser"/> get a fully
    /// resolved principal.
    /// </summary>
    public static WebApplication UseCardscapeMcp(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapCardscapeHealthChecks(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new
        {
            status = "healthy",
            service = "Cardscape.Mcp",
            timestamp = DateTime.UtcNow
        }));

        app.MapGet("/health/ready", () => Results.Ok(new
        {
            status = "ready",
            service = "Cardscape.Mcp",
            timestamp = DateTime.UtcNow
        }));

        return app;
    }
}
