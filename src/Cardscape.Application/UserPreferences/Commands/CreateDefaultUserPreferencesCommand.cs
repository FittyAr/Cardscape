using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.UserPreferences.DTOs;
using Cardscape.Application.UserPreferences.Mappings;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;
using AppearanceModeAlias = Cardscape.Domain.UserPreferences.AppearanceMode;
using UserPreferencesAggregate = Cardscape.Domain.UserPreferences.UserPreferences;

namespace Cardscape.Application.UserPreferences.Commands;

/// <summary>
/// Idempotently creates the caller's preferences row with
/// the project defaults (Radzen <c>default</c> theme +
/// follow-system mode). Called by the Blazor client on the
/// first <c>GET /api/users/me/preferences</c> that returns
/// 404. If the row already exists, the handler returns the
/// existing row's DTO without touching it — safe to retry.
/// </summary>
public sealed record CreateDefaultUserPreferencesCommand() : IMessage;

public static class CreateDefaultUserPreferencesCommandHandler
{
    public static async Task<Result<UserPreferencesDto>> Handle(
        CreateDefaultUserPreferencesCommand _,
        IUserPreferencesRepository preferences,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellation)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<UserPreferencesDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        UserId userId = new(currentUser.Id.Value);

        UserPreferencesAggregate? existing = await preferences.GetByIdAsync(userId, cancellation);
        if (existing is not null)
        {
            // Idempotent path: a concurrent call already
            // created the row. Return the existing one
            // without bumping CreatedAt / UpdatedAt.
            return Result.Success(existing.MapToDto());
        }

        var createResult = UserPreferencesAggregate.Create(
            userId: userId,
            themeName: UserPreferencesAggregate.DefaultThemeName,
            mode: AppearanceModeAlias.System,
            at: clock.UtcNow);

        if (createResult.IsFailure)
        {
            return Result.Failure<UserPreferencesDto>(createResult.Error);
        }

        await preferences.AddAsync(createResult.Value, cancellation);
        await unitOfWork.SaveChangesAsync(cancellation);

        return Result.Success(createResult.Value.MapToDto());
    }
}
