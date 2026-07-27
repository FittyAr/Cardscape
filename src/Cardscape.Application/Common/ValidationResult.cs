using Cardscape.Domain.Common;

namespace Cardscape.Application.Common;

/// <summary>
/// FluentValidation errors packaged as a <see cref="DomainError"/>
/// so the rest of the application sees a uniform failure shape.
/// </summary>
public static class ValidationErrorMapper
{
    public static DomainError ToDomainError(FluentValidation.Results.ValidationFailure failure) =>
        DomainError.Validation(failure.ErrorCode ?? "validation", failure.ErrorMessage);
}
