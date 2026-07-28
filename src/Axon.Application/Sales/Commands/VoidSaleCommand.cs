using Axon.Application.Common.Behaviors;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Sales.Commands;

// Anular (no eliminar) una venta: deja Sale.Status = Voided con motivo y usuario
// registrados, sin borrar el registro (ver Matriz de Roles y Permisos v2, regla
// transversal A). A diferencia de ReturnSaleCommand, no revierte stock ni caja —
// esa reversión física es responsabilidad de la Devolución, un flujo distinto.
//
// SupervisorPin es opcional: si quien pide la anulación ya tiene el permiso
// sales:void (Administrador/Propietario), no hace falta. Si no lo tiene (p. ej.
// un Cajero), debe traer el PIN de un Administrador/Propietario para habilitarla
// (ver SupervisorAuthorization).
public record VoidSaleCommand(Guid SaleId, string Reason, string? SupervisorPin = null)
    : IRequest<MediatRUnit>, IAuditableRequest
{
    public string AuditAction => "Sale.Void";
    public Guid? AuditEntityId => SaleId;
}
