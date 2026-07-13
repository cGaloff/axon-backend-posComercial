using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Sales.Commands;

public record ConfirmSalePaymentCommand(Guid SaleId) : IRequest<MediatRUnit>;
