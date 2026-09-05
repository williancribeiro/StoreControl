using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StoreControl.Domain.Catalog;
using StoreControl.Domain.Cash;
using StoreControl.Domain.Inventory;
using StoreControl.Domain.Parties;
using StoreControl.Domain.Purchasing;
using StoreControl.Domain.Sales;
using StoreControl.Domain.Stores;
using StoreControl.Infrastructure.Identity;
using StoreControl.Infrastructure.Persistence.Models;

namespace StoreControl.Infrastructure.Persistence;

public sealed class StoreControlDbContext(DbContextOptions<StoreControlDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    internal DbSet<FoundationRecord> FoundationRecords => Set<FoundationRecord>();

    internal DbSet<CacheRevision> CacheRevisions => Set<CacheRevision>();

    internal DbSet<DailySalesReportRow> DailySalesReportRows => Set<DailySalesReportRow>();

    internal DbSet<StockPositionReportRow> StockPositionReportRows => Set<StockPositionReportRow>();

    public DbSet<Store> Stores => Set<Store>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<StockBalance> StockBalances => Set<StockBalance>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();

    public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();

    public DbSet<StockTransferStatusHistory> StockTransferStatusHistory => Set<StockTransferStatusHistory>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();

    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();

    public DbSet<PurchaseReceiptItem> PurchaseReceiptItems => Set<PurchaseReceiptItem>();

    public DbSet<PurchaseStatusHistory> PurchaseStatusHistory => Set<PurchaseStatusHistory>();

    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();

    public DbSet<CashSession> CashSessions => Set<CashSession>();

    public DbSet<CashMovement> CashMovements => Set<CashMovement>();

    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    public DbSet<SalePayment> SalePayments => Set<SalePayment>();

    public DbSet<SaleStatusHistory> SaleStatusHistory => Set<SaleStatusHistory>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserStoreRole> UserStoreRoles => Set<UserStoreRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("dbo");
        modelBuilder.Entity<ApplicationUser>().ToTable("Users", "security");
        modelBuilder.Entity<ApplicationRole>().ToTable("Roles", "security");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims", "security");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins", "security");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles", "security");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims", "security");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens", "security");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StoreControlDbContext).Assembly);

        // These tables have transactional cache-generation triggers; direct OUTPUT is incompatible.
        modelBuilder.Entity<Category>().ToTable(table => table.UseSqlOutputClause(false));
        modelBuilder.Entity<Product>().ToTable(table => table.UseSqlOutputClause(false));
        modelBuilder.Entity<Store>().ToTable(table => table.UseSqlOutputClause(false));
        modelBuilder.Entity<StockBalance>().ToTable(table => table.UseSqlOutputClause(false));
        modelBuilder.Entity<Sale>().ToTable(table => table.UseSqlOutputClause(false));
        modelBuilder.Entity<SaleItem>().ToTable(table => table.UseSqlOutputClause(false));
    }
}
