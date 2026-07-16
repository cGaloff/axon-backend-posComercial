using MediatR;

namespace Axon.Application.Inventory.Queries;

public record GetAttributeDefinitionsQuery(Guid? CategoryId = null) : IRequest<List<AttributeDefinitionDto>>;

public record AttributeDefinitionDto(
    Guid Id,
    string Key,
    string Label,
    string Type,
    List<string>? Options,
    Guid? CategoryId,
    bool IsFilterable,
    int SortOrder);
