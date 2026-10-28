using Axon.Domain.Entities;
using FluentValidation;

namespace Axon.Application.Tenants.Commands;

public class ExtendTenantSubscriptionCommandValidator : AbstractValidator<ExtendTenantSubscriptionCommand>
{
    public ExtendTenantSubscriptionCommandValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty();

        RuleFor(x => x.ExtensionDays)
            .GreaterThan(0)
            .When(x => x.ExtensionDays.HasValue);

        RuleFor(x => x.BillingCycle)
            .Must(cycle => BillingCycles.IsValid(cycle!.Trim()))
            .WithMessage($"Ciclo de facturación inválido. Valores permitidos: {string.Join(", ", BillingCycles.All)}")
            .When(x => !string.IsNullOrWhiteSpace(x.BillingCycle));

        // Sin días ni ciclo no hay de dónde saber cuánto extender.
        RuleFor(x => x)
            .Must(x => x.ExtensionDays.HasValue || !string.IsNullOrWhiteSpace(x.BillingCycle))
            .WithMessage("Debe indicar extensionDays o billingCycle")
            .WithName("ExtensionDays");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Amount.HasValue);

        RuleFor(x => x.TransactionReference)
            .MaximumLength(200);
    }
}
