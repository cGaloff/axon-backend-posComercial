using FluentValidation;

namespace Axon.Application.Tenants.Commands;

public class UpdateTenantSubscriptionCommandValidator : AbstractValidator<UpdateTenantSubscriptionCommand>
{
    public UpdateTenantSubscriptionCommandValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(100);

        // No se valida que la fecha sea futura: la landing también usa este
        // endpoint para cortar el acceso de inmediato ante un impago.
        RuleFor(x => x.Plan)
            .Must(plan => RegisterTenantCommandValidator.ValidPlans.Contains(plan!))
            .WithMessage("Plan inválido")
            .When(x => x.Plan is not null);
    }
}
