using Axon.Domain.Entities;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Sales;
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
    DbSet<Sale> Sales { get; }
    DbSet<SaleReturn> SaleReturns { get; }
}
