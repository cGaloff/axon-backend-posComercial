using System.Text.Json;
using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Inventory;

public class Product
{
    private readonly List<ProductTax> _taxes = new();

    public Guid Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public decimal Cost { get; private set; }
    public int Stock { get; private set; }
    public int MinStock { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid UnitId { get; private set; }
    public Dictionary<string, JsonElement> Attributes { get; private set; } = new();
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<ProductTax> Taxes => _taxes;

    private Product()
    {
    }

    public static Product Create(
        string sku,
        string name,
        decimal price,
        decimal cost,
        int minStock,
        Guid categoryId,
        Guid unitId)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainException("El SKU es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del producto es obligatorio.");
        }

        if (price <= 0)
        {
            throw new DomainException("El precio debe ser mayor a cero.");
        }

        if (cost < 0)
        {
            throw new DomainException("El costo no puede ser negativo.");
        }

        if (minStock < 0)
        {
            throw new DomainException("El stock mínimo no puede ser negativo.");
        }

        return new Product
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Name = name,
            Description = string.Empty,
            Price = price,
            Cost = cost,
            Stock = 0,
            MinStock = minStock,
            CategoryId = categoryId,
            UnitId = unitId,
            Attributes = new Dictionary<string, JsonElement>(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Reemplaza por completo el conjunto de impuestos configurados para el producto.
    // El porcentaje es libre (lo define el usuario, sin whitelist); no se permite
    // asignar el mismo TaxTypeId más de una vez en la misma llamada.
    public void SetTaxes(IEnumerable<(Guid TaxTypeId, decimal Percentage)> taxes)
    {
        var requested = taxes.ToList();

        if (requested.Select(t => t.TaxTypeId).Distinct().Count() != requested.Count)
        {
            throw new DomainException("No se puede asignar el mismo impuesto más de una vez al mismo producto.");
        }

        var newTaxes = requested
            .Select(t => ProductTax.Create(Id, t.TaxTypeId, t.Percentage))
            .ToList();

        _taxes.Clear();
        _taxes.AddRange(newTaxes);
    }

    public void UpdateAverageCost(decimal newAverageCost)
    {
        if (newAverageCost < 0)
        {
            throw new DomainException("El costo promedio no puede ser negativo.");
        }

        Cost = newAverageCost;
    }

    public void AdjustStock(int quantity)
    {
        var newStock = Stock + quantity;

        if (newStock < 0)
        {
            throw new DomainException(
                $"Stock insuficiente. Stock actual: {Stock}, cantidad solicitada: {quantity}");
        }

        Stock = newStock;
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0)
        {
            throw new DomainException("El precio debe ser mayor a cero.");
        }

        Price = newPrice;
    }

    public void UpdateDetails(
        string name,
        string description,
        decimal price,
        decimal cost,
        int minStock,
        Guid categoryId,
        Guid unitId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del producto es obligatorio.");
        }

        if (price <= 0)
        {
            throw new DomainException("El precio debe ser mayor a cero.");
        }

        if (cost < 0)
        {
            throw new DomainException("El costo no puede ser negativo.");
        }

        if (minStock < 0)
        {
            throw new DomainException("El stock mínimo no puede ser negativo.");
        }

        Name = name;
        Description = description;
        Price = price;
        Cost = cost;
        MinStock = minStock;
        CategoryId = categoryId;
        UnitId = unitId;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void SetAttributes(Dictionary<string, JsonElement> attributes)
    {
        Attributes = attributes;
    }
}
