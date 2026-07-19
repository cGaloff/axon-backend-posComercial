using MediatR;

namespace Axon.Application.Suppliers.Commands;

public record CreateSupplierCommand(
    string Name,
    string? Nit,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    string? City,
    int PaymentTermDays = 30) : IRequest<Guid>;
