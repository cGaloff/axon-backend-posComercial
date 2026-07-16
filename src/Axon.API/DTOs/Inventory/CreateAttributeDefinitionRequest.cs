namespace Axon.API.DTOs.Inventory;

public record CreateAttributeDefinitionRequest(
    string Key,
    string Label,
    string Type,
    List<string>? Options,
    Guid? CategoryId,
    bool IsFilterable,
    int SortOrder);
