using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Workspaces.Commands;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using FluentValidation;
using Wolverine;

namespace Cardscape.Application.Authentication.Commands;

/// <summary>
/// Registers a new user through a workspace invitation link and joins
/// them to the workspace in the same transaction. The token is the
/// invitee's proof, so this path stays open when public registration
/// is disabled; the email must be the invited one.
/// </summary>
public sealed record RegisterInvitedUserCommand(
    string Email,
    string DisplayName,
    string Password,
    string InvitationToken) : IMessage;

public static class RegisterInvitedUserCommandHandler
{
    public static async Task<Result<AuthResponse>> HandleAsync(
        RegisterInvitedUserCommand command,
        IInvitationService invitations,
        IWorkspaceInvitationRepository invitationRepository,
        IWorkspaceRepository workspaces,
        IUserRepository users,
        IPasswordHasher hasher,
        IUnitOfWork unitOfWork,
        ITokenService tokens,
        IClock clock,
        IValidator<RegisterUserCommand> validator,
        CancellationToken cancellationToken)
    {
        var validation = await invitations.ValidateAsync(command.InvitationToken, clock.UtcNow, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<AuthResponse>(validation.Error);
        }

        WorkspaceInvitation? invitation = await invitationRepository.GetByIdAsync(
            validation.Value.InvitationId, cancellationToken);
        if (invitation is null)
        {
            return Result.Failure<AuthResponse>(DomainError.NotFound(
                "workspaces.invitation.not_found", "Invitation was not found."));
        }

        if (!string.Equals(command.Email?.Trim(), invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<AuthResponse>(DomainError.Forbidden(
                "workspaces.invitation.email_mismatch",
                "This invitation was sent to a different email address."));
        }

        var userResult = await RegisterUserCommandHandler.CreateUserAsync(
            new RegisterUserCommand(command.Email ?? string.Empty, command.DisplayName, command.Password),
            users, hasher, clock, validator, cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(userResult.Error);
        }

        var user = userResult.Value;
        // The invitation was mailed to this address and its token came back.
        user.MarkEmailVerified(clock.UtcNow);
        await users.AddAsync(user, cancellationToken);

        // Saves the user, the membership and the accepted invitation
        // together; if the workspace is gone nothing is written.
        var joined = await AcceptWorkspaceInvitationCommandHandler.RedeemAsync(
            invitation, user.Id.Value, user.Email.Value, workspaces, unitOfWork, clock, cancellationToken);
        if (joined.IsFailure)
        {
            users.Remove(user);
            return Result.Failure<AuthResponse>(joined.Error);
        }

        var access = tokens.IssueAccessToken(user, ["user"]);
        return Result.Success(new AuthResponse(access, UserSummary.From(user)));
    }
}
