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
        // The name form wins when both are supplied.
        group.MapGet("/", async (
            string? q,
            Guid? boardId,
            int? kind,
            string? kindName,
            int? page,
            int? pageSize,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            SearchHitKind? resolvedKind = null;
            if (!string.IsNullOrWhiteSpace(kindName))
            {
                if (Enum.TryParse<SearchHitKind>(kindName, ignoreCase: true, out SearchHitKind parsed))
                {
                    resolvedKind = parsed;
                }
                else
                {
                    return Results.BadRequest(new
                    {
                        code = "search.kind_invalid",
                        message = $"Unknown kind '{kindName}'. Valid values: {string.Join(", ", Enum.GetNames<SearchHitKind>())}."
                    });
                }
            }
            else if (kind is int kindInt && Enum.IsDefined(typeof(SearchHitKind), kindInt))
            {
                resolvedKind = (SearchHitKind)kindInt;
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
