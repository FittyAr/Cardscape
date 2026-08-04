using Cardscape.Application.Authentication.Commands;
using Cardscape.Application.Authentication.Queries;
using FluentValidation;

namespace Cardscape.Application.Authentication.Validations;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            // Reject the bare-word defaults (the top-100
            // most-leaked list). The list is local so the
            // registration path does not leak candidate
            // passwords to a third-party breach-check
            // service; the trade-off is documented in
            // CommonPasswords.cs.
            .Must(p => !CommonPasswords.Set.Contains(p ?? string.Empty))
            .WithMessage("Password is on the breached-passwords list; pick a different one.");
    }
}

public sealed class LoginUserQueryValidator : AbstractValidator<LoginUserQuery>
{
    public LoginUserQueryValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
