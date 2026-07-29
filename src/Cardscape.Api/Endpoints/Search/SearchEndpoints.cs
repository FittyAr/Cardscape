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

        group.MapGet("/", async (
            string? q,
            Guid? boardId,
            SearchHitKind? kind,
            int? page,
            int? pageSize,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<SearchPageDto>>(
                new SearchQuery(
                    Query: q ?? string.Empty,
                    BoardId: boardId,
                    Kind: kind,
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
