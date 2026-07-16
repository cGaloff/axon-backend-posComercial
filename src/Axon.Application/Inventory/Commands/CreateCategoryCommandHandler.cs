using Axon.Application.Interfaces;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Commands;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCategoryCommandHandler(IApplicationDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var nameInUse = await _dbContext.Categories.AnyAsync(
            c => EF.Functions.ILike(c.Name, request.Name), cancellationToken);

        if (nameInUse)
        {
            throw new DomainException($"Ya existe una categoría con el nombre '{request.Name}'");
        }

        var category = Category.Create(request.Name, request.Description);

        _dbContext.Categories.Add(category);
        await _unitOfWork.CommitAsync(cancellationToken);

        return category.Id;
    }
}
