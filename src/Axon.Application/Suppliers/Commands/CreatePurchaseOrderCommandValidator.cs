using FluentValidation;

namespace Axon.Application.Suppliers.Commands;

public class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty();

        RuleFor(x => x.SupplierInvoiceNumber)
            .MaximumLength(100);

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("La orden debe tener al menos un ítem");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.QuantityOrdered).GreaterThan(0);
            item.RuleFor(i => i.UnitCost).GreaterThan(0);
        });
    }
}
