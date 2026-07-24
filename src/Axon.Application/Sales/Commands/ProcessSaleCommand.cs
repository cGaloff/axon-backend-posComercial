using Axon.Domain.Entities.Sales;
using MediatR;

namespace Axon.Application.Sales.Commands;

public record SaleItemRequest(Guid ProductId, int Quantity, decimal Discount = 0);

// PaymentMethod: catálogo heredado del modelo anterior (Cash/Card/Transfer/Credit).
// PENDIENTE DE CONFIRMAR CON NEGOCIO si este es el catálogo real de métodos de
// pago que se deben poder combinar en una misma venta (ver resumen del prompt 4).
public record SalePaymentRequest(PaymentMethod Method, decimal Amount, decimal? AmountTendered = null);

public record ProcessSaleCommand(
    List<SaleItemRequest> Items,
    List<SalePaymentRequest> Payments,
    Guid CashRegisterId,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerEmail,
    string? Notes) : IRequest<ProcessSaleResult>;
