using FluentValidation;

namespace Axon.Application.Inventory.Commands;

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Price)
            .GreaterThan(0);

        RuleFor(x => x.Cost)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.MinStock)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.CategoryId)
            .NotEmpty();

        RuleFor(x => x.UnitId)
            .NotEmpty();
    }
}
