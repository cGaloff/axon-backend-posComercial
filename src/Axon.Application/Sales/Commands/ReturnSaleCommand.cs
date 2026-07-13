using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Sales.Commands;

public record ReturnSaleCommand(Guid SaleId, string Reason, Guid ReturnedBy) : IRequest<MediatRUnit>;
