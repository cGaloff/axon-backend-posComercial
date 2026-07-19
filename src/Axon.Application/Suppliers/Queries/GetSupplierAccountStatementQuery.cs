using MediatR;

namespace Axon.Application.Suppliers.Queries;

public record GetSupplierAccountStatementQuery(
    Guid SupplierId,
    DateTime? From = null,
    DateTime? To = null) : IRequest<SupplierAccountStatementDto>;

public record SupplierAccountStatementDto(
    Guid SupplierId,
    string SupplierName,
    decimal TotalPurchased,
    decimal TotalPaid,
    decimal Balance,
    List<SupplierTransactionDto> Transactions);

public record SupplierTransactionDto(
    DateTime Date,
    string Type,
    string Description,
    decimal Amount,
    decimal RunningBalance);
