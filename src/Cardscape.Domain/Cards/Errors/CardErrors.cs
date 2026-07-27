using Cardscape.Domain.Common;

namespace Cardscape.Domain.Cards.Errors;

public static class CardErrors
{
    public static readonly DomainError NotFound =
        DomainError.NotFound("cards.not_found", "Card was not found.");

    public static readonly DomainError Archived =
        DomainError.Conflict("cards.archived", "Card is archived and cannot be modified.");

    public static readonly DomainError AlreadyAssigned =
        DomainError.Conflict("cards.already_assigned", "User is already assigned to this card.");

    public static readonly DomainError NotAssigned =
        DomainError.Conflict("cards.not_assigned", "User is not assigned to this card.");
}
