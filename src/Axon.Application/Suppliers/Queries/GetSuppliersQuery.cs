using MediatR;

namespace Axon.Application.Suppliers.Queries;

public record GetSuppliersQuery(
    string? Search = null,
    bool IncludeInactive = false) : IRequest<List<SupplierDto>>;

public record SupplierDto(
    Guid Id,
    string Name,
    string? Nit,
    string? ContactName,
    string? Phone,
    string? Email,
    string? City,
    int PaymentTermDays,
    bool IsActive,
    int ProductCount,
    decimal TotalDebt);
