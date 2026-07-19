using Axon.Domain.Entities.Sales;
using MediatR;

namespace Axon.Application.Sales.Commands;

public record SaleItemRequest(Guid ProductId, int Quantity, decimal Discount = 0);

public record ProcessSaleCommand(
    List<SaleItemRequest> Items,
    PaymentMethod PaymentMethod,
    Guid CashRegisterId,
    decimal AmountPaid,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerEmail,
    string? Notes) : IRequest<ProcessSaleResult>;
