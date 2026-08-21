using FluentValidation;

namespace Axon.Application.Tenants.Commands;

public class ConfirmTenantRegistrationCommandValidator : AbstractValidator<ConfirmTenantRegistrationCommand>
{
    public ConfirmTenantRegistrationCommandValidator()
    {
        RuleFor(x => x.PendingRegistrationId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches("^[0-9]{6}$")
            .WithMessage("El código debe tener 6 dígitos");
    }
}
