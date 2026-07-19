using FluentValidation;

namespace Axon.Application.Suppliers.Commands;

public class ReceivePurchaseOrderCommandValidator : AbstractValidator<ReceivePurchaseOrderCommand>
{
    public ReceivePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId)
            .NotEmpty();

        RuleFor(x => x.Items)
            .NotEmpty();

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.PurchaseOrderItemId).NotEmpty();
            item.RuleFor(i => i.QuantityReceived).GreaterThan(0);
        });
    }
}
