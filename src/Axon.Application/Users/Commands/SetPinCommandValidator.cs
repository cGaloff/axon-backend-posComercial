using FluentValidation;

namespace Axon.Application.Users.Commands;

public class SetPinCommandValidator : AbstractValidator<SetPinCommand>
{
    public SetPinCommandValidator()
    {
        RuleFor(x => x.Pin)
            .NotEmpty()
            .Matches("^[0-9]{4,6}$")
            .WithMessage("El PIN debe ser numérico, de 4 a 6 dígitos.");
    }
}
