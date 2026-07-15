using FluentValidation;

namespace Axon.Application.CashRegister.Commands;

public class CloseCashSessionCommandValidator : AbstractValidator<CloseCashSessionCommand>
{
    public CloseCashSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty();

        RuleFor(x => x.ClosedBy)
            .NotEmpty();

        RuleFor(x => x.CountedAmount)
            .GreaterThanOrEqualTo(0);
    }
}
