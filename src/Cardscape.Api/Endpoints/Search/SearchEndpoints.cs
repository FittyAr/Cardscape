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

        // Query-string binding does not use the JSON enum converter,
        // so parse names explicitly and reject numeric CLR values.
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
                string? definedName = Enum.GetNames<SearchHitKind>()
                    .FirstOrDefault(name => string.Equals(name, kind, StringComparison.OrdinalIgnoreCase));
                if (definedName is not null)
                {
                    resolvedKind = Enum.Parse<SearchHitKind>(definedName);
                }
                else
                {
                    return ApiProblemResults.BadRequest(
                        "search.kind_invalid",
                        $"Unknown kind '{kind}'. Valid values: {string.Join(", ", Enum.GetNames<SearchHitKind>())}.");
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
            return result.IsSuccess ? Results.Ok(result.Value) : DomainErrorResults.ToProblem(result.Error);
        });

        return app;
    }

}
