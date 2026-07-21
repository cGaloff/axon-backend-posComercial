using Axon.Domain.Entities;
using Axon.Domain.Entities.CashRegister;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Entities.Suppliers;
using Microsoft.EntityFrameworkCore;
using CashRegisterEntity = Axon.Domain.Entities.CashRegister.CashRegister;

namespace Axon.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<Category> Categories { get; }
    DbSet<Unit> Units { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Product> Products { get; }
    DbSet<AttributeDefinition> AttributeDefinitions { get; }
    DbSet<InventoryMovement> InventoryMovements { get; }
    DbSet<StockAlert> StockAlerts { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleReturn> SaleReturns { get; }
    DbSet<CashRegisterEntity> CashRegisters { get; }
    DbSet<CashSession> CashSessions { get; }
    DbSet<CashMovement> CashMovements { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseReceipt> PurchaseReceipts { get; }
    DbSet<SupplierPayment> SupplierPayments { get; }
    DbSet<ProductSupplier> ProductSuppliers { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
}
