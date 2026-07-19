using FluentValidation;

namespace Axon.Application.Suppliers.Commands;

public class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Nit)
            .MaximumLength(20)
            .When(x => x.Nit is not null);

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => x.Email is not null);

        RuleFor(x => x.PaymentTermDays)
            .GreaterThan(0);
    }
}
