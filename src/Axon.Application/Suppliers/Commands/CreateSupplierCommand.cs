using Axon.Domain.Entities.Suppliers;
using MediatR;

namespace Axon.Application.Suppliers.Commands;

public record CreateSupplierCommand(
    string Name,
    SupplierDocumentType DocumentType,
    string DocumentNumber,
    string ContactName,
    string Phone,
    string Email,
    string? Address,
    string? City) : IRequest<Guid>;
