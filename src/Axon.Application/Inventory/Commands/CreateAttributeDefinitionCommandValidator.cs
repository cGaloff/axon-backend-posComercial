using FluentValidation;

namespace Axon.Application.Inventory.Commands;

public class CreateAttributeDefinitionCommandValidator : AbstractValidator<CreateAttributeDefinitionCommand>
{
    private static readonly string[] ValidTypes = { "text", "select", "boolean", "number" };

    public CreateAttributeDefinitionCommandValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Label)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Type)
            .Must(type => ValidTypes.Contains(type))
            .WithMessage($"El tipo debe ser uno de: {string.Join(", ", ValidTypes)}");

        RuleFor(x => x.Options)
            .Must(options => options is { Count: > 0 })
            .When(x => x.Type == "select")
            .WithMessage("Los atributos de tipo 'select' requieren al menos una opción");
    }
}
