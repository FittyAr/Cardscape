using Cardscape.Domain.Common;

namespace Cardscape.Domain.Boards.Errors;

public static class BoardErrors
{
    public static readonly DomainError NotFound =
        DomainError.NotFound("boards.not_found", "Board was not found.");

    public static readonly DomainError Archived =
        DomainError.Conflict("boards.archived", "Board is archived and cannot be modified.");

    public static readonly DomainError AlreadyMember =
        DomainError.Conflict("boards.already_member", "User is already a member of this board.");

    public static readonly DomainError NotMember =
        DomainError.Forbidden("boards.not_member", "You are not a member of this board.");

    public static readonly DomainError Forbidden =
        DomainError.Forbidden("boards.forbidden", "You do not have permission to perform this action.");

    public static readonly DomainError LastAdmin =
        DomainError.Conflict("boards.members.last_admin", "The board must have at least one admin.");

    public static readonly DomainError MemberNotFound =
        DomainError.NotFound("boards.members.not_found", "The user is not a member of this board.");

    public static readonly DomainError MemberNotInWorkspace =
        DomainError.Validation("boards.members.not_in_workspace",
            "Only members of the board's workspace can be added to the board.");

    public static readonly DomainError InvalidMemberRole =
        DomainError.Validation("boards.members.invalid_role", "The board role is not valid.");
}
