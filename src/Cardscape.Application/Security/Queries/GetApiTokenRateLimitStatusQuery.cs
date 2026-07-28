using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Security;
using Wolverine;

namespace Cardscape.Application.Security.Queries;

public sealed record GetApiTokenRateLimitStatusQuery(Guid TokenId) : IMessage;

public static class GetApiTokenRateLimitStatusQueryHandler
{
    public static async Task<Result<ApiTokenRateLimitStatusDto>> Handle(
        GetApiTokenRateLimitStatusQuery query,
        IApiTokenService tokens,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<ApiTokenRateLimitStatusDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var status = await tokens.GetRateLimitStatusAsync(
            currentUser.Id,
            new ApiTokenId(query.TokenId),
            clock.UtcNow,
            cancellationToken);

        if (status.IsFailure)
        {
            return Result.Failure<ApiTokenRateLimitStatusDto>(status.Error);
        }

        return Result.Success(new ApiTokenRateLimitStatusDto(
            status.Value.TokenId,
            status.Value.RateLimitPerHour,
            status.Value.BurstSize,
            status.Value.AvailableTokens,
            status.Value.At));
    }
}

public sealed record ApiTokenRateLimitStatusDto(
    Guid TokenId,
    int RateLimitPerHour,
    int BurstSize,
    double AvailableTokens,
    DateTimeOffset At);
