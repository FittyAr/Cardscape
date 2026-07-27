using Cardscape.Domain.Common;

namespace Cardscape.Domain.Checklists.Errors;

public static class ChecklistErrors
{
    public static readonly DomainError NotFound =
        DomainError.NotFound("checklists.not_found", "Checklist was not found.");

    public static readonly DomainError ItemNotFound =
        DomainError.NotFound("checklists.item_not_found", "Checklist item was not found.");
}
