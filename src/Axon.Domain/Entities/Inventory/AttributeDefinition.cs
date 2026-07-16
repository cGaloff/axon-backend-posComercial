using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Inventory;

public class AttributeDefinition
{
    private static readonly string[] ValidTypes = { "text", "select", "boolean", "number" };

    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public List<string>? Options { get; private set; }
    public Guid? CategoryId { get; private set; }
    public bool IsFilterable { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private AttributeDefinition()
    {
    }

    public static AttributeDefinition Create(string key, string label, string type, Guid? categoryId)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainException("La clave del atributo es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainException("La etiqueta del atributo es obligatoria.");
        }

        if (!ValidTypes.Contains(type))
        {
            throw new DomainException($"Tipo de atributo inválido: '{type}'.");
        }

        return new AttributeDefinition
        {
            Id = Guid.NewGuid(),
            Key = key.Trim().ToLowerInvariant().Replace(' ', '_'),
            Label = label,
            Type = type,
            CategoryId = categoryId,
            IsFilterable = false,
            SortOrder = 0,
            IsActive = true
        };
    }

    public void Configure(List<string>? options, bool isFilterable, int sortOrder)
    {
        if (Type == "select" && (options is null || options.Count == 0))
        {
            throw new DomainException("Los atributos de tipo 'select' requieren al menos una opción.");
        }

        Options = options;
        IsFilterable = isFilterable;
        SortOrder = sortOrder;
    }
}
