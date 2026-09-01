using Cardscape.Application.Automation;
using Cardscape.Domain.Boards;
using FluentValidation;

namespace Cardscape.Application.Automation.Validations;

/// <summary>
/// BETA-7-#8 — see test-results/BETA-TEST-REPORT.md.
/// <c>AutomationTrigger</c> and <c>AutomationAction</c> are
/// enums; the default JSON binder accepts any int (so
/// <c>trigger: 99</c> was happily creating a rule that would
/// never fire). The domain method has no range check because
/// the enum is statically typed — the failure mode is
/// introduced by the JSON deserialiser. We close the gap in
/// a FluentValidation validator so the response is a clean
/// 400 instead of a 201 with a broken rule.
/// </summary>
public sealed class CreateBoardAutomationRuleCommandValidator
    : AbstractValidator<CreateBoardAutomationRuleCommand>
{
    public CreateBoardAutomationRuleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Trigger)
            .Must(Enum.IsDefined)
            .WithMessage("Trigger value is not a recognised AutomationTrigger.");

        RuleFor(x => x.Action)
            .Must(Enum.IsDefined)
            .WithMessage("Action value is not a recognised AutomationAction.");
    }
}
