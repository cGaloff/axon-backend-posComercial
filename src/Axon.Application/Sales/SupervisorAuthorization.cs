using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Sales;

// "Autorización de supervisor": si quien pide la acción (p. ej. un Cajero) ya
// tiene el permiso requerido, se autoriza a sí mismo (AuthorizedBy queda null,
// no hizo falta nadie más). Si no lo tiene, debe traer el PIN de un
// Administrador/Propietario activo que sí tenga el permiso — se busca entre
// TODOS los usuarios elegibles (no hace falta que el Cajero sepa el email del
// supervisor, solo que el supervisor teclee su PIN). Matriz de Roles y
// Permisos v2, rol Cajero.
public static class SupervisorAuthorization
{
    public static async Task<Guid?> ResolveAsync(
        IApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        ICurrentUserContext currentUser,
        string requiredPermission,
        string? supervisorPin,
        CancellationToken cancellationToken)
    {
        if (currentUser.HasPermission(requiredPermission))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(supervisorPin))
        {
            throw new DomainException(
                "Esta acción requiere autorización de un Administrador o Propietario. Ingrese su PIN.");
        }

        var eligibleUsers = await dbContext.Users
            .Include(u => u.Role)
            .ThenInclude(r => r!.Permissions)
            .Where(u => u.IsActive && u.PinHash != null)
            .ToListAsync(cancellationToken);

        var authorizer = eligibleUsers
            .Where(u => u.Role!.Permissions.Any(p => p.Key == requiredPermission))
            .FirstOrDefault(u => passwordHasher.Verify(supervisorPin, u.PinHash!));

        if (authorizer is null)
        {
            throw new DomainException("PIN de autorización inválido.");
        }

        return authorizer.Id;
    }
}
