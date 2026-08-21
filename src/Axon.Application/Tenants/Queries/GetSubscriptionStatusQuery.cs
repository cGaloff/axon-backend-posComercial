using MediatR;

namespace Axon.Application.Tenants.Queries;

public record GetSubscriptionStatusQuery : IRequest<SubscriptionStatusResult>;
