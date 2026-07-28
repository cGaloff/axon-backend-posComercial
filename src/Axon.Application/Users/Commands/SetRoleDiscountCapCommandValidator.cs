using FluentValidation;

namespace Axon.Application.Users.Commands;

public class SetRoleDiscountCapCommandValidator : AbstractValidator<SetRoleDiscountCapCommand>
{
    public SetRoleDiscountCapCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty();

        RuleFor(x => x.MaxDiscountPercentage)
            .InclusiveBetween(0, 100)
            .When(x => x.MaxDiscountPercentage.HasValue);
    }
}
