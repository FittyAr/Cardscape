using Cardscape.Domain.Common;

namespace Cardscape.Domain.Labels.Errors;

public static class LabelErrors
{
    public static readonly DomainError NotFound =
        DomainError.NotFound("labels.not_found", "Label was not found.");
}
