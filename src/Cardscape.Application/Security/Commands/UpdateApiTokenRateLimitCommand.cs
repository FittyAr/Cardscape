using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Security;
using Wolverine;

namespace Cardscape.Application.Security.Commands;

public sealed record UpdateApiTokenRateLimitCommand(
    Guid TokenId,
    int RateLimitPerHour,
    int BurstSize) : IMessage;

public static class UpdateApiTokenRateLimitCommandHandler
{
    public static async Task<Result<ApiTokenRateLimitDto>> Handle(
        UpdateApiTokenRateLimitCommand command,
        IApiTokenService tokens,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<ApiTokenRateLimitDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        if (command.RateLimitPerHour < 0)
        {
            return Result.Failure<ApiTokenRateLimitDto>(DomainError.Validation(
                "security.api_token.rate_limit_invalid",
                "Rate limit per hour must be 0 (disabled) or a positive number."));
        }

        if (command.BurstSize < 0)
        {
            return Result.Failure<ApiTokenRateLimitDto>(DomainError.Validation(
                "security.api_token.burst_size_invalid",
                "Burst size must be 0 or a positive number."));
        }

        var update = await tokens.UpdateRateLimitAsync(
            currentUser.Id,
            new ApiTokenId(command.TokenId),
            command.RateLimitPerHour,
            command.BurstSize,
            cancellationToken);

        if (update.IsFailure)
        {
            return Result.Failure<ApiTokenRateLimitDto>(update.Error);
        }

        var status = await tokens.GetRateLimitStatusAsync(
            currentUser.Id,
            new ApiTokenId(command.TokenId),
            clock.UtcNow,
            cancellationToken);

        if (status.IsFailure)
        {
            return Result.Failure<ApiTokenRateLimitDto>(status.Error);
        }

        return Result.Success(new ApiTokenRateLimitDto(
            status.Value.TokenId,
            status.Value.RateLimitPerHour,
            status.Value.BurstSize,
            status.Value.AvailableTokens,
            status.Value.At));
    }
}

/// <summary>Wire shape of the rate-limit configuration +
/// current bucket state. Returned by the PATCH and GET rate-limit
/// endpoints.</summary>
public sealed record ApiTokenRateLimitDto(
    Guid TokenId,
    int RateLimitPerHour,
    int BurstSize,
    double AvailableTokens,
    DateTimeOffset At);
