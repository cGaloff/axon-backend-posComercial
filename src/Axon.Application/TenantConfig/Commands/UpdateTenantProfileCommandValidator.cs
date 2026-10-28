using Axon.Domain.Entities;
using FluentValidation;

namespace Axon.Application.TenantConfig.Commands;

public class UpdateTenantProfileCommandValidator : AbstractValidator<UpdateTenantProfileCommand>
{
    public UpdateTenantProfileCommandValidator()
    {
        RuleFor(x => x.BusinessName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.LegalName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Nit)
            .MaximumLength(20);

        RuleFor(x => x.Phone)
            .MaximumLength(50);

        RuleFor(x => x.Address)
            .MaximumLength(500);

        RuleFor(x => x.City)
            .MaximumLength(100);

        RuleFor(x => x.State)
            .MaximumLength(100);

        RuleFor(x => x.TaxRegime)
            .Must(TaxRegimes.IsValid)
            .WithMessage($"Régimen tributario inválido. Valores permitidos: {string.Join(", ", TaxRegimes.All)}");
    }
}
