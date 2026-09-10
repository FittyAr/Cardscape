using Cardscape.Application.BackgroundJobs;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.BackgroundJobs;

public static class BackgroundJobEndpoints
{
    public static IEndpointRouteBuilder MapBackgroundJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/background-jobs")
            .RequireAuthorization("AdminOnly")
            .WithTags("Background jobs");

        group.MapGet("/dead-letter", async (int? skip, int? take, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<BackgroundJobSummaryDto>>>(
                new ListDeadLetterBackgroundJobsQuery(skip ?? 0, take ?? 50), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : DomainErrorResults.ToProblem(result.Error);
        });

        return app;
    }

}
