using FluentValidation;

namespace Axon.Application.Inventory.Commands;

public class SetMermaApprovalThresholdCommandValidator : AbstractValidator<SetMermaApprovalThresholdCommand>
{
    public SetMermaApprovalThresholdCommandValidator()
    {
        RuleFor(x => x.Threshold)
            .GreaterThanOrEqualTo(0);
    }
}
