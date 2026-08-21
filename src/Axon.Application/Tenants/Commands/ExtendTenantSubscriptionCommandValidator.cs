using FluentValidation;

namespace Axon.Application.Tenants.Commands;

public class ExtendTenantSubscriptionCommandValidator : AbstractValidator<ExtendTenantSubscriptionCommand>
{
    public ExtendTenantSubscriptionCommandValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty();

        RuleFor(x => x.ExtensionDays)
            .GreaterThan(0);
    }
}
