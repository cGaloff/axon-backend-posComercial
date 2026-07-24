using Axon.Domain.Entities;
using Axon.Domain.Entities.CashRegister;
using Axon.Domain.Entities.Invoicing;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Entities.Suppliers;
using Axon.Domain.Entities.Taxes;
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
    DbSet<TaxType> TaxTypes { get; }
    DbSet<InventoryMovement> InventoryMovements { get; }
    DbSet<StockAlert> StockAlerts { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleReturn> SaleReturns { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<CashRegisterEntity> CashRegisters { get; }
    DbSet<CashSession> CashSessions { get; }
    DbSet<CashMovement> CashMovements { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseReceipt> PurchaseReceipts { get; }
    DbSet<SupplierPayment> SupplierPayments { get; }
    DbSet<ProductSupplier> ProductSuppliers { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    // Consecutivo de factura, atómico y aislado por tenant: en Postgres usa la
    // secuencia invoice_number_seq del schema del tenant (nextval() no tiene
    // condiciones de carrera entre transacciones concurrentes, y cada tenant
    // tiene su propia secuencia en su propio schema, sin ninguna forma de
    // colisionar con la de otro tenant). Ver TenantDbContext para el fallback
    // usado en proveedores no relacionales (solo pruebas).
    Task<long> GetNextInvoiceNumberAsync(CancellationToken cancellationToken);
}
