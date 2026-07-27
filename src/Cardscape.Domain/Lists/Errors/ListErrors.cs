using Cardscape.Domain.Common;

namespace Cardscape.Domain.Lists.Errors;

public static class ListErrors
{
    public static readonly DomainError NotFound =
        DomainError.NotFound("lists.not_found", "List was not found.");

    public static readonly DomainError Archived =
        DomainError.Conflict("lists.archived", "List is archived and cannot be modified.");
}
