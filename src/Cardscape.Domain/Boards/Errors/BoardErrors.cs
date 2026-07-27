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
        DomainError.Conflict("boards.last_admin", "The board must have at least one admin.");
}
