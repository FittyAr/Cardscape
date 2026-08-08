using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.UserPreferences.DTOs;
using Cardscape.Application.UserPreferences.Mappings;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.UserPreferences;
using FluentValidation;
using Wolverine;
using UserPreferencesAggregate = Cardscape.Domain.UserPreferences.UserPreferences;

namespace Cardscape.Application.UserPreferences.Commands;

/// <summary>
/// Updates the caller's preferences row. Either
/// <see cref="ThemeName"/> or <see cref="Mode"/> (or both)
/// can be supplied; <c>null</c> means "leave unchanged". The
/// Blazor client always sends both, but the partial-update
/// shape keeps the API friendly to other future callers
/// (e.g. an MCP tool that wants to flip the mode without
/// re-sending the theme).
///
/// If the caller has no row yet, the handler returns
/// <c>NotFound</c> — the Blazor client is expected to call
/// <c>CreateDefaultUserPreferencesCommand</c> first and
/// retry. (An alternative is upsert here, but explicit is
/// cheaper to debug.)
/// </summary>
public sealed record UpdateUserPreferencesCommand(
    string? ThemeName,
    AppearanceMode? Mode) : IMessage;

public static class UpdateUserPreferencesCommandHandler
{
    /// <summary>Whitelist of theme names the handler will accept.
    /// Mirrors the 12 entries in
    /// <c>src/Cardscape.Web/Theming/ThemeCatalog.All</c>. If a
    /// future maintainer adds a 13th entry on the Web side,
    /// this list must be updated in lockstep — the
    /// <c>UserPreferencesValidator</c> tests will fail loudly
    /// if the divergence is wider than one entry.</summary>
    public static readonly IReadOnlyCollection<string> ValidThemeNames = new[]
    {
        "default", "dark",
        "humanistic", "humanistic-dark",
        "material", "material-dark",
        "software", "software-dark",
        "standard", "standard-dark",
        "cardscape-classic", "cardscape-classic-dark",
    };

    public static async Task<Result<UserPreferencesDto>> Handle(
        UpdateUserPreferencesCommand command,
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

        UserPreferencesAggregate? prefs = await preferences.GetByIdAsync(userId, cancellation);
        if (prefs is null)
        {
            return Result.Failure<UserPreferencesDto>(DomainError.NotFound(
                "members.user_preferences.not_found",
                "No preferences row exists for this user. Create one first."));
        }

        Result updateResult = prefs.Update(
            themeName: command.ThemeName,
            mode: command.Mode,
            validThemeNames: ValidThemeNames,
            at: clock.UtcNow);

        if (updateResult.IsFailure)
        {
            return Result.Failure<UserPreferencesDto>(updateResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellation);
        return Result.Success(prefs.MapToDto());
    }
}

/// <summary>FluentValidation rules for <see cref="UpdateUserPreferencesCommand"/>.
/// Lives next to the command so the rule list stays in sync
/// with the field set. The theme-name rule mirrors
/// <see cref="UpdateUserPreferencesCommandHandler.ValidThemeNames"/>.</summary>
public sealed class UpdateUserPreferencesValidator : AbstractValidator<UpdateUserPreferencesCommand>
{
    public UpdateUserPreferencesValidator()
    {
        When(c => c.ThemeName is not null, () =>
        {
            RuleFor(c => c.ThemeName!)
                .NotEmpty()
                .Must(name => UpdateUserPreferencesCommandHandler.ValidThemeNames.Contains(name))
                .WithMessage($"Theme must be one of: {string.Join(", ", UpdateUserPreferencesCommandHandler.ValidThemeNames)}.");
        });

        When(c => c.Mode is not null, () =>
        {
            RuleFor(c => c.Mode!.Value)
                .IsInEnum()
                .WithMessage("Mode must be Light, Dark, or System.");
        });
    }
}
