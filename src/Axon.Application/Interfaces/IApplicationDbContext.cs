using Axon.Domain.Entities;
using Axon.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Unit> Units { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Product> Products { get; }
    DbSet<AttributeDefinition> AttributeDefinitions { get; }
    DbSet<InventoryMovement> InventoryMovements { get; }
    DbSet<StockAlert> StockAlerts { get; }
}
