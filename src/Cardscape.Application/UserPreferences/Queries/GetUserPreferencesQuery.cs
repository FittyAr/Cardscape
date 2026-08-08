using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.UserPreferences.DTOs;
using Cardscape.Application.UserPreferences.Mappings;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;
using UserPreferencesAggregate = Cardscape.Domain.UserPreferences.UserPreferences;

namespace Cardscape.Application.UserPreferences.Queries;

/// <summary>
/// Reads the current user's preferences row. Returns
/// <c>Result&lt;UserPreferencesDto&gt;.Success(null)</c>
/// (DTO is null) when the user has no row yet — the Blazor
/// client uses the null as the signal to call
/// <c>CreateDefaultUserPreferencesCommand</c> and then
/// re-fetch. Returning null from a success (rather than a
/// <c>NotFound</c> error) keeps the GET idempotent: a fresh
/// user is not an error, it's just uninitialised.
/// </summary>
public sealed record GetUserPreferencesQuery() : IMessage;

public static class GetUserPreferencesQueryHandler
{
    public static async Task<Result<UserPreferencesDto?>> Handle(
        GetUserPreferencesQuery _,
        IUserPreferencesRepository preferences,
        ICurrentUser currentUser,
        CancellationToken cancellation)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<UserPreferencesDto?>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        UserPreferencesAggregate? prefs = await preferences.GetByIdAsync(
            new UserId(currentUser.Id.Value), cancellation);

        return Result.Success<UserPreferencesDto?>(prefs?.MapToDto());
    }
}
