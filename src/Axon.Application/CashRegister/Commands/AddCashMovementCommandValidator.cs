using Axon.Domain.Entities.CashRegister;
using FluentValidation;

namespace Axon.Application.CashRegister.Commands;

public class AddCashMovementCommandValidator : AbstractValidator<AddCashMovementCommand>
{
    public AddCashMovementCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Type)
            .Must(t => t == CashMovementType.ManualIncome || t == CashMovementType.Expense)
            .WithMessage("Solo se permiten movimientos manuales desde este endpoint");
    }
}
