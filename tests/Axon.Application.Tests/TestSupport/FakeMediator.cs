using Axon.Application.Invoicing.Commands;
using MediatR;

namespace Axon.Application.Tests.TestSupport;

// Mediador mínimo para pruebas: delega IssueInvoiceCommand al handler real
// (así los tests de ProcessSaleCommandHandler/ConfirmSalePaymentCommandHandler
// ejercitan la emisión de factura real, no una versión simulada), y falla
// explícitamente ante cualquier otro tipo de request no anticipado.
public class FakeMediator : IMediator
{
    private readonly IssueInvoiceCommandHandler _issueInvoiceCommandHandler;

    public FakeMediator(IssueInvoiceCommandHandler issueInvoiceCommandHandler)
    {
        _issueInvoiceCommandHandler = issueInvoiceCommandHandler;
    }

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is IssueInvoiceCommand command)
        {
            var result = await _issueInvoiceCommandHandler.Handle(command, cancellationToken);
            return (TResponse)(object)result;
        }

        throw new NotSupportedException($"FakeMediator no maneja requests de tipo {request.GetType().Name}");
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
        throw new NotSupportedException("FakeMediator no soporta Send<TRequest>(TRequest).");

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeMediator no soporta Send(object).");

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeMediator no soporta streams.");

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeMediator no soporta streams.");

    public Task Publish(object notification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeMediator no soporta Publish.");

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification =>
        throw new NotSupportedException("FakeMediator no soporta Publish.");
}
