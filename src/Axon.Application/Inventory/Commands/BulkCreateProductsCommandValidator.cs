using FluentValidation;

namespace Axon.Application.Inventory.Commands;

public class BulkCreateProductsCommandValidator : AbstractValidator<BulkCreateProductsCommand>
{
    public BulkCreateProductsCommandValidator()
    {
        RuleFor(x => x.Products)
            .NotEmpty()
            .WithMessage("El archivo no contiene productos para importar.");
    }
}
