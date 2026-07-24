using FluentValidation;

namespace Axon.Application.Inventory.Commands;

public class UpdateTaxTypeCommandValidator : AbstractValidator<UpdateTaxTypeCommand>
{
    public UpdateTaxTypeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Code)
            .MaximumLength(20);
    }
}
