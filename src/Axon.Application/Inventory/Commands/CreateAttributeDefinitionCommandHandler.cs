using Axon.Application.Interfaces;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Commands;

public class CreateAttributeDefinitionCommandHandler : IRequestHandler<CreateAttributeDefinitionCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAttributeDefinitionCommandHandler(IApplicationDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateAttributeDefinitionCommand request, CancellationToken cancellationToken)
    {
        var normalizedKey = request.Key.Trim().ToLowerInvariant().Replace(' ', '_');

        var keyInUse = await _dbContext.AttributeDefinitions.AnyAsync(
            a => a.Key == normalizedKey && a.CategoryId == request.CategoryId,
            cancellationToken);

        if (keyInUse)
        {
            throw new DomainException($"Ya existe un atributo con la clave '{normalizedKey}' para esta categoría");
        }

        var attributeDefinition = AttributeDefinition.Create(request.Key, request.Label, request.Type, request.CategoryId);
        attributeDefinition.Configure(request.Options, request.IsFilterable, request.SortOrder);

        _dbContext.AttributeDefinitions.Add(attributeDefinition);
        await _unitOfWork.CommitAsync(cancellationToken);

        return attributeDefinition.Id;
    }
}
