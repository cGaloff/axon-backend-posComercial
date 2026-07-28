using Axon.Application.Common.Behaviors;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Sales.Commands;

// SupervisorPin es opcional: si quien pide la devolución ya tiene el permiso
// sales:void (Administrador/Propietario), no hace falta. Si no lo tiene (p. ej.
// un Cajero), debe traer el PIN de un Administrador/Propietario (ver
// SupervisorAuthorization).
public record ReturnSaleCommand(Guid SaleId, string Reason, string? SupervisorPin = null)
    : IRequest<MediatRUnit>, IAuditableRequest
{
    public string AuditAction => "Sale.Return";
    public Guid? AuditEntityId => SaleId;
}
