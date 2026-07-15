using FluentValidation;

namespace Axon.Application.CashRegister.Commands;

public class OpenCashSessionCommandValidator : AbstractValidator<OpenCashSessionCommand>
{
    public OpenCashSessionCommandValidator()
    {
        RuleFor(x => x.CashRegisterId)
            .NotEmpty();

        RuleFor(x => x.OpenedBy)
            .NotEmpty();

        RuleFor(x => x.InitialAmount)
            .GreaterThanOrEqualTo(0);
    }
}
