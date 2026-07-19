using Axon.Application.Inventory.DTOs;
using MediatR;

namespace Axon.Application.Inventory.Queries;

public record GetProductByIdQuery(Guid Id) : IRequest<ProductDto>;
