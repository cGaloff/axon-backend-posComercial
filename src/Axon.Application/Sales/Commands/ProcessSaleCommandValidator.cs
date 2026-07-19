using FluentValidation;

namespace Axon.Application.Sales.Commands;

public class ProcessSaleCommandValidator : AbstractValidator<ProcessSaleCommand>
{
    public ProcessSaleCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("La venta debe tener al menos un ítem");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.Discount).GreaterThanOrEqualTo(0);
        });

        RuleFor(x => x.CashRegisterId)
            .NotEmpty();

        RuleFor(x => x.AmountPaid)
            .GreaterThanOrEqualTo(0);

        // Si PaymentMethod == Cash, AmountPaid >= Total se valida en el handler,
        // porque el total depende de los productos cargados (no se conoce aquí).
    }
}
