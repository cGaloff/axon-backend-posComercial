using Axon.Domain.Entities;
using FluentValidation;

namespace Axon.Application.Tenants.Commands;

public class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    // internal para que el validador del PATCH de suscripción use la misma lista.
    // La prueba gratis no es un plan aparte: usa "basic" y la gobierna la fecha.
    internal static readonly string[] ValidPlans = { "basic", "pro", "enterprise" };

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

        // Opcional: si no viene, el tenant queda sin vencimiento. Se rechaza una
        // fecha ya pasada porque el tenant nacería bloqueado, que casi siempre
        // es un error de quien aprovisiona.
        RuleFor(x => x.SubscriptionEndsAt)
            .Must(date => Tenant.NormalizeToUtc(date) > DateTime.UtcNow)
            .WithMessage("La fecha de vencimiento debe ser futura")
            .When(x => x.SubscriptionEndsAt.HasValue);
    }
}
