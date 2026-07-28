using MediatR;

namespace Axon.Application.Inventory.Queries;

public record GetPendingInventoryMovementsQuery : IRequest<List<PendingInventoryMovementDto>>;

public record PendingInventoryMovementDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    int Quantity,
    decimal EstimatedValue,
    string Reason,
    Guid CreatedBy,
    DateTime CreatedAt);
