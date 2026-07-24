using FluentValidation;

namespace Axon.Application.Inventory.Commands;

public class CreateTaxTypeCommandValidator : AbstractValidator<CreateTaxTypeCommand>
{
    public CreateTaxTypeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Code)
            .MaximumLength(20);
    }
}
