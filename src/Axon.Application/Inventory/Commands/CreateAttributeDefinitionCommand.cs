using MediatR;

namespace Axon.Application.Inventory.Commands;

public record CreateAttributeDefinitionCommand(
    string Key,
    string Label,
    string Type,
    List<string>? Options,
    Guid? CategoryId,
    bool IsFilterable = true,
    int SortOrder = 0) : IRequest<Guid>;
