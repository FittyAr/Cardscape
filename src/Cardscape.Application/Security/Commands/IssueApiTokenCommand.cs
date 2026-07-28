using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Security;
using Wolverine;

namespace Cardscape.Application.Security.Commands;

public sealed record IssueApiTokenCommand(
    string Name,
    IReadOnlyCollection<string> Scopes,
    DateTimeOffset? ExpiresAt,
    int? RateLimitPerHour = null,
    int? BurstSize = null) : IMessage;

public static class IssueApiTokenCommandHandler
{
    public static async Task<Result<ApiTokenIssuanceDto>> Handle(
        IssueApiTokenCommand command,
        IApiTokenService tokens,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<ApiTokenIssuanceDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        // Validate inputs here (instead of throwing from the
        // service) so the global exception middleware turns
        // these into proper 400 responses, not 500s.
        var nameResult = ApiTokenName.Create(command.Name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<ApiTokenIssuanceDto>(nameResult.Error);
        }

        var scopesResult = ApiTokenScopes.Create(command.Scopes);
        if (scopesResult.IsFailure)
        {
            return Result.Failure<ApiTokenIssuanceDto>(scopesResult.Error);
        }

        var issuance = await tokens.IssueAsync(
            currentUser.Id,
            command.Name,
            command.Scopes,
            command.ExpiresAt,
            command.RateLimitPerHour,
            command.BurstSize,
            cancellationToken);

        return Result.Success(new ApiTokenIssuanceDto(
            issuance.Id.Value,
            issuance.CleartextSecret));
    }
}

/// <summary>
/// DTO returned to the Web UI exactly once at issuance. The
/// cleartext secret is the only time the caller ever sees it.
/// </summary>
public sealed record ApiTokenIssuanceDto(Guid Id, string CleartextSecret);
