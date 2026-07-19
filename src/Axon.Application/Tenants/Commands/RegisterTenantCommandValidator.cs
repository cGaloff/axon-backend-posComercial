using FluentValidation;

namespace Axon.Application.Tenants.Commands;

public class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    private static readonly string[] ValidPlans = { "basic", "pro", "enterprise" };

    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.BusinessName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("El slug solo puede contener letras minúsculas, números y guiones");

        RuleFor(x => x.OwnerEmail)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.OwnerPassword)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(x => x.Plan)
            .NotEmpty()
            .Must(plan => ValidPlans.Contains(plan))
            .WithMessage("Plan inválido");
    }
}
