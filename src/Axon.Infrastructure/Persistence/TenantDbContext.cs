using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Entities.CashRegister;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Entities.Suppliers;
using Axon.Domain.Interfaces;
using Axon.Infrastructure.Persistence.Configurations;
using Axon.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using CashRegisterEntity = Axon.Domain.Entities.CashRegister.CashRegister;

namespace Axon.Infrastructure.Persistence;

public class TenantDbContext : DbContext, IApplicationDbContext
{
    private readonly ITenantContext _tenantContext;

    public TenantDbContext(DbContextOptions<TenantDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<StockAlert> StockAlerts => Set<StockAlert>();

    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();

    public DbSet<CashRegisterEntity> CashRegisters => Set<CashRegisterEntity>();

    public DbSet<CashSession> CashSessions => Set<CashSession>();

    public DbSet<CashMovement> CashMovements => Set<CashMovement>();

    public DbSet<TenantConfig> TenantConfigs => Set<TenantConfig>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();

    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();

    public DbSet<ProductSupplier> ProductSuppliers => Set<ProductSupplier>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new TenantSchemaInterceptor(_tenantContext));
        optionsBuilder.UseSnakeCaseNamingConvention();
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionConfiguration());
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
        modelBuilder.ApplyConfiguration(new UnitConfiguration());
        modelBuilder.ApplyConfiguration(new WarehouseConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new AttributeDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryMovementConfiguration());
        modelBuilder.ApplyConfiguration(new StockAlertConfiguration());
        modelBuilder.ApplyConfiguration(new SaleConfiguration());
        modelBuilder.ApplyConfiguration(new SaleReturnConfiguration());
        modelBuilder.ApplyConfiguration(new CashRegisterConfiguration());
        modelBuilder.ApplyConfiguration(new CashSessionConfiguration());
        modelBuilder.ApplyConfiguration(new CashMovementConfiguration());
        modelBuilder.ApplyConfiguration(new TenantConfigConfiguration());
        modelBuilder.ApplyConfiguration(new SupplierConfiguration());
        modelBuilder.ApplyConfiguration(new PurchaseOrderConfiguration());
        modelBuilder.ApplyConfiguration(new PurchaseReceiptConfiguration());
        modelBuilder.ApplyConfiguration(new SupplierPaymentConfiguration());
        modelBuilder.ApplyConfiguration(new ProductSupplierConfiguration());
    }
}
