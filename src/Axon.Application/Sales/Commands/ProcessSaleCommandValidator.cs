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

            item.RuleFor(i => i.Discount)
                .GreaterThanOrEqualTo(0)
                .When(i => i.Discount.HasValue);

            item.RuleFor(i => i.DiscountPercentage)
                .InclusiveBetween(0, 100)
                .When(i => i.DiscountPercentage.HasValue);

            // Igual que el descuento general de la venta: monto fijo o
            // porcentaje para ESTE producto, nunca ambos a la vez.
            item.RuleFor(i => i)
                .Must(i => i.Discount is null || i.DiscountPercentage is null)
                .WithMessage("El descuento de un producto debe darse como monto o como porcentaje, no ambos.");
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

        // Documento del cliente es opcional (si no se da, queda vacío — ver
        // Sale.Create) — pero si se da uno de los dos campos, el otro también
        // es obligatorio.
        RuleFor(x => x.CustomerDocumentNumber)
            .MaximumLength(30)
            .NotEmpty()
            .WithMessage("El número de documento del cliente es obligatorio si se indica el tipo de documento.")
            .When(x => x.CustomerDocumentType.HasValue);

        RuleFor(x => x.CustomerDocumentType)
            .NotNull()
            .WithMessage("El tipo de documento del cliente es obligatorio si se indica el número de documento.")
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerDocumentNumber));

        // Descuento general de la venta: monto fijo o porcentaje, nunca ambos a
        // la vez (el handler decide cuál usar y no sabría cómo combinarlos).
        RuleFor(x => x)
            .Must(x => x.SaleDiscountAmount is null || x.SaleDiscountPercentage is null)
            .WithMessage("El descuento general de la venta debe darse como monto o como porcentaje, no ambos.");

        RuleFor(x => x.SaleDiscountAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.SaleDiscountAmount.HasValue);

        RuleFor(x => x.SaleDiscountPercentage)
            .InclusiveBetween(0, 100)
            .When(x => x.SaleDiscountPercentage.HasValue);

        // La suma de Payments.Amount contra el total de la venta (con tolerancia de
        // redondeo) se valida en el handler/dominio, porque el total depende de los
        // productos cargados (no se conoce aquí).
    }
}
