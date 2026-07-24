using Axon.Domain.Entities.Suppliers;
using MediatR;

namespace Axon.Application.Suppliers.Queries;

public record GetSuppliersQuery(
    string? Search = null,
    bool IncludeInactive = false) : IRequest<List<SupplierDto>>;

public record SupplierDto(
    Guid Id,
    string Name,
    SupplierDocumentType DocumentType,
    string DocumentNumber,
    string ContactName,
    string Phone,
    string Email,
    string? Address,
    string? City,
    bool IsActive,
    int ProductCount,
    decimal TotalDebt);
