using FluentValidation;

namespace Axon.Application.Suppliers.Commands;

public class RegisterSupplierPaymentCommandValidator : AbstractValidator<RegisterSupplierPaymentCommand>
{
    private static readonly string[] ValidPaymentMethods = { "Cash", "Transfer", "Check", "Other" };

    public RegisterSupplierPaymentCommandValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0);

        RuleFor(x => x.PaymentMethod)
            .Must(m => ValidPaymentMethods.Contains(m))
            .WithMessage($"El método de pago debe ser uno de: {string.Join(", ", ValidPaymentMethods)}");
    }
}
