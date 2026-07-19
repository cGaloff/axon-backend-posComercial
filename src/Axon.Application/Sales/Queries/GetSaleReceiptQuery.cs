using MediatR;

namespace Axon.Application.Sales.Queries;

public record GetSaleReceiptQuery(Guid SaleId) : IRequest<byte[]>;
