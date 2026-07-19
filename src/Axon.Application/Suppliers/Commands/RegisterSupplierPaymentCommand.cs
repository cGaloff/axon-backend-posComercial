using MediatR;

namespace Axon.Application.Suppliers.Commands;

public record RegisterSupplierPaymentCommand(
    Guid SupplierId,
    decimal Amount,
    string PaymentMethod,
    string? Reference,
    string? Notes) : IRequest<Guid>;
