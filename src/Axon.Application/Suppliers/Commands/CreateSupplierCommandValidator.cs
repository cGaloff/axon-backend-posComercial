using FluentValidation;

namespace Axon.Application.Suppliers.Commands;

public class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DocumentType)
            .IsInEnum();

        RuleFor(x => x.DocumentNumber)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.ContactName)
            .NotEmpty()
            .MaximumLength(200)
            .Must(name => name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
            .WithMessage("El nombre de contacto debe incluir nombre y apellido.");

        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
