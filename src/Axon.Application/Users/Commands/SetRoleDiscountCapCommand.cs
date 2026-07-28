using Axon.Application.Common.Behaviors;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Users.Commands;

// Null = sin tope (Propietario/Administrador). Con valor = % máximo de
// descuento que un usuario con este rol puede aplicar sin autorización de un
// rol con tope superior (Matriz de Roles y Permisos v2, regla transversal C).
public record SetRoleDiscountCapCommand(Guid RoleId, decimal? MaxDiscountPercentage) : IRequest<MediatRUnit>, IAuditableRequest
{
    public string AuditAction => "Role.SetDiscountCap";
    public Guid? AuditEntityId => RoleId;
}
