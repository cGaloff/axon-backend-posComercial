using System.Text.Json;
using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Inventory;

public class Product
{
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
    public decimal TaxPercentage { get; private set; }

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
        Guid unitId,
        decimal taxPercentage = 0)
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

        ValidateTaxPercentage(taxPercentage);

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
            CreatedAt = DateTime.UtcNow,
            TaxPercentage = taxPercentage
        };
    }

    public void UpdateTaxPercentage(decimal taxPercentage)
    {
        ValidateTaxPercentage(taxPercentage);

        TaxPercentage = taxPercentage;
    }

    private static void ValidateTaxPercentage(decimal taxPercentage)
    {
        if (taxPercentage != 0 && taxPercentage != 5 && taxPercentage != 19)
        {
            throw new DomainException("Porcentaje de IVA inválido. Valores permitidos: 0, 5, 19");
        }
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
