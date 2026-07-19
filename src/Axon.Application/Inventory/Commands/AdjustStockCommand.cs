using Axon.Domain.Entities.Inventory;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public record AdjustStockCommand(
    Guid ProductId,
    int Quantity,
    InventoryMovementType Type,
    string Reason) : IRequest<MediatRUnit>;
