using Axon.Application.Tenants.Commands;
using MediatR;

namespace Axon.Application.Tests.TestSupport;

// Mediador mínimo para probar ConfirmTenantRegistrationCommandHandler sin
// ejercitar la creación real del tenant (RegisterTenantCommandHandler hace
// ExecuteSqlRawAsync/SqlQueryRaw contra el schema del tenant, que el
// proveedor InMemory de EF no soporta — requeriría Postgres real). Solo
// registra el comando recibido y devuelve un resultado fijo, mismo espíritu
// que FakeMediator (que sí delega IssueInvoiceCommand a su handler real
// porque ese caso sí es ejercitable con InMemory).
public class FakeRegisterTenantMediator : IMediator
{
    public RegisterTenantCommand? LastCommand { get; private set; }
    public RegisterTenantResult Result { get; set; } =
        new(Guid.NewGuid(), "tenant_test", "test-slug", "Test Business");

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is RegisterTenantCommand command)
        {
            LastCommand = command;
            return Task.FromResult((TResponse)(object)Result);
        }

        throw new NotSupportedException($"FakeRegisterTenantMediator no maneja requests de tipo {request.GetType().Name}");
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
        throw new NotSupportedException("FakeRegisterTenantMediator no soporta Send<TRequest>(TRequest).");

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeRegisterTenantMediator no soporta Send(object).");

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeRegisterTenantMediator no soporta streams.");

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeRegisterTenantMediator no soporta streams.");

    public Task Publish(object notification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeRegisterTenantMediator no soporta Publish.");

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification =>
        throw new NotSupportedException("FakeRegisterTenantMediator no soporta Publish.");
}
