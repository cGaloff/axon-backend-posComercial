using Axon.Domain.Entities.Sales;

namespace Axon.Application.Sales.Commands;

public record ProcessSaleResult(
    Guid SaleId,
    string SaleNumber,
    decimal Total,
    decimal Change,
    SaleStatus Status,
    byte[] PdfReceipt);
