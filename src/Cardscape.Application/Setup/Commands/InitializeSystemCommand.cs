using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Application.Authentication;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Members.Errors.UserErrors;

namespace Cardscape.Application.Setup.Commands;

/// <summary>
/// Initializes a brand-new Cardscape instance by creating the first system administrator account
/// and an initial default workspace. Fails closed (409 Conflict) if any user already exists.
/// </summary>
public sealed record InitializeSystemCommand(
    string AdminDisplayName,
    string AdminEmail,
    string AdminPassword,
    string? InstanceTitle,
    string? InitialWorkspaceName) : IMessage;

public static class InitializeSystemCommandHandler
{
    public static async Task<Result<AuthResponse>> HandleAsync(
        InitializeSystemCommand command,
        IUserRepository users,
        IWorkspaceRepository workspaces,
        IPasswordHasher hasher,
        IUnitOfWork unitOfWork,
        ITokenService tokens,
        IClock clock,
        ISystemSettingsService settingsService,
        CancellationToken cancellationToken)
    {
        // 1. Enforce that setup can only run on a fresh, uninitialized database.
        if (await users.AnyAsync(cancellationToken))
        {
            return Result.Failure<AuthResponse>(
                DomainError.Conflict("Setup.AlreadyInitialized", "The system has already been initialized."));
        }


        // 2. Validate email and display name via domain value objects.
        var emailResult = EmailAddress.Create(command.AdminEmail);
        if (emailResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(emailResult.Error);
        }

        var displayNameResult = DisplayName.Create(command.AdminDisplayName);
        if (displayNameResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(displayNameResult.Error);
        }

        // 3. Password strength and common-password dictionary checks.
        if (string.IsNullOrWhiteSpace(command.AdminPassword) || command.AdminPassword.Length < 8)
        {
            return Result.Failure<AuthResponse>(InvalidPassword(
                "Password must be at least 8 characters long."));
        }

        if (CommonPasswords.Set.Contains(command.AdminPassword))
        {
            return Result.Failure<AuthResponse>(InvalidPassword(
                "Password is on the breached-passwords list; pick a different one."));
        }

        // 4. Create and register the initial administrator user.
        var hash = hasher.Hash(command.AdminPassword);
        var userResult = User.Register(
            UserId.New(),
            emailResult.Value,
            displayNameResult.Value,
            hash,
            clock.UtcNow);

        if (userResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(userResult.Error);
        }

        User adminUser = userResult.Value;
        adminUser.SetAdmin(true, clock.UtcNow);
        await users.AddAsync(adminUser, cancellationToken);

        // 5. Create the initial workspace.
        string wsName = string.IsNullOrWhiteSpace(command.InitialWorkspaceName)
            ? "Principal"
            : command.InitialWorkspaceName.Trim();

        var wsNameResult = WorkspaceName.Create(wsName);
        if (wsNameResult.IsSuccess)
        {
            var wsResult = Workspace.Create(
                WorkspaceId.New(),
                wsNameResult.Value,
                adminUser.Id.Value,
                Region.Unspecified,
                clock.UtcNow);

            if (wsResult.IsSuccess)
            {
                await workspaces.AddAsync(wsResult.Value, cancellationToken);
            }
        }

        // 6. Update instance title if specified.
        if (!string.IsNullOrWhiteSpace(command.InstanceTitle))
        {
            await settingsService.UpdateSettingsAsync(
                new UpdateSystemSettingsRequest(
                    command.InstanceTitle.Trim(),
                    AllowPublicRegistration: true,
                    DefaultLanguage: "es",
                    JwtAccessTokenMinutes: 60),
                adminUser.Email.Value,
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 7. Mint admin JWT token.
        var access = tokens.IssueAccessToken(adminUser, ["admin", "user"]);

        return Result.Success(new AuthResponse(
            access,
            new UserSummary(
                adminUser.Id.Value,
                adminUser.Email.Value,
                adminUser.DisplayName.Value)));
    }
}
