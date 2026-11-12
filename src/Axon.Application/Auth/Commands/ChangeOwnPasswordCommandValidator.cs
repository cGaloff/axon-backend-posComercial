using FluentValidation;

namespace Axon.Application.Auth.Commands;

public class ChangeOwnPasswordCommandValidator : AbstractValidator<ChangeOwnPasswordCommand>
{
    public ChangeOwnPasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .WithMessage("La contraseña debe tener al menos 8 caracteres")
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("La nueva contraseña debe ser distinta de la actual");
    }
}
