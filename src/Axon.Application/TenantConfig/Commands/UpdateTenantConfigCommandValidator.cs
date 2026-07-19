using FluentValidation;

namespace Axon.Application.TenantConfig.Commands;

public class UpdateTenantConfigCommandValidator : AbstractValidator<UpdateTenantConfigCommand>
{
    public UpdateTenantConfigCommandValidator()
    {
        RuleFor(x => x.BusinessName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Nit)
            .MaximumLength(20)
            .When(x => x.Nit is not null);

        RuleFor(x => x.Phone)
            .MaximumLength(50)
            .When(x => x.Phone is not null);

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => x.Email is not null);
    }
}
