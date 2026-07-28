using FluentValidation;

namespace Axon.Application.Sales.Commands;

public class VoidSaleCommandValidator : AbstractValidator<VoidSaleCommand>
{
    public VoidSaleCommandValidator()
    {
        RuleFor(x => x.SaleId)
            .NotEmpty();

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de anulación es obligatorio.")
            .MaximumLength(500);
    }
}
