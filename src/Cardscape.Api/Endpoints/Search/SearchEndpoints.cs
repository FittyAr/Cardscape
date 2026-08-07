using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Search;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Search;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/search").RequireAuthorization().WithTags("Search");

        // BETA-7-#14 — see test-results/BETA-TEST-REPORT.md.
        // The query binder doesn't run the
        // `JsonStringEnumConverter` (that only applies to
        // JSON bodies), so `?kind=card` returned 400. The
        // endpoint now accepts BOTH the integer form
        // (`?kind=0`, kept for back-compat) and the name
        // form (`?kind=card`, the new friendly surface).
        // We take `kind` as a raw `string?` and parse it
        // inside the handler — declaring it as `int?` would
        // make ASP.NET's binder reject the request with 400
        // before the handler ever runs, which is exactly the
        // bug we are fixing.
        group.MapGet("/", async (
            string? q,
            Guid? boardId,
            string? kind,
            int? page,
            int? pageSize,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            SearchHitKind? resolvedKind = null;
            if (!string.IsNullOrWhiteSpace(kind))
            {
                if (Enum.TryParse<SearchHitKind>(kind, ignoreCase: true, out SearchHitKind parsed) &&
                    Enum.IsDefined(typeof(SearchHitKind), parsed))
                {
                    resolvedKind = parsed;
                }
                else
                {
                    return Results.BadRequest(new
                    {
                        code = "search.kind_invalid",
                        message = $"Unknown kind '{kind}'. Valid values: {string.Join(", ", Enum.GetNames<SearchHitKind>())}."
                    });
                }
            }

            var result = await bus.InvokeAsync<Result<SearchPageDto>>(
                new SearchQuery(
                    Query: q ?? string.Empty,
                    BoardId: boardId,
                    Kind: resolvedKind,
                    Page: page ?? 1,
                    PageSize: pageSize ?? 20),
                ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        return app;
    }

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.Unauthenticated => Results.Unauthorized(),
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
