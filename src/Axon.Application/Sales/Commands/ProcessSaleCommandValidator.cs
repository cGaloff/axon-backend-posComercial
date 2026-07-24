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

        RuleFor(x => x.Payments)
            .NotEmpty()
            .WithMessage("La venta debe tener al menos una forma de pago");

        RuleForEach(x => x.Payments).ChildRules(payment =>
        {
            payment.RuleFor(p => p.Amount).GreaterThan(0);

            payment.RuleFor(p => p.AmountTendered)
                .GreaterThanOrEqualTo(p => p.Amount)
                .When(p => p.AmountTendered.HasValue)
                .WithMessage("El monto entregado no puede ser menor al monto del pago.");
        });

        RuleFor(x => x.CashRegisterId)
            .NotEmpty();

        // La suma de Payments.Amount contra el total de la venta (con tolerancia de
        // redondeo) se valida en el handler/dominio, porque el total depende de los
        // productos cargados (no se conoce aquí).
    }
}
