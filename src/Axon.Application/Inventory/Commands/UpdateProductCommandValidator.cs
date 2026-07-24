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

        RuleForEach(x => x.Taxes).ChildRules(tax =>
        {
            tax.RuleFor(t => t.TaxTypeId).NotEmpty();
            tax.RuleFor(t => t.Percentage).GreaterThanOrEqualTo(0);
        });

        RuleFor(x => x.Taxes)
            .Must(taxes => taxes is null || taxes.Select(t => t.TaxTypeId).Distinct().Count() == taxes.Count)
            .WithMessage("No se puede asignar el mismo impuesto más de una vez al mismo producto.");
    }
}
