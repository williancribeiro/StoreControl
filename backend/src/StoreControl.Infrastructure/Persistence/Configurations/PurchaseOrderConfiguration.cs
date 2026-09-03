using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Purchasing;
using StoreControl.Infrastructure.Identity;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders", "purchasing", table =>
        {
            table.HasCheckConstraint("CK_PurchaseOrders_Status", "[Status] BETWEEN 1 AND 6");
            table.HasCheckConstraint("CK_PurchaseOrders_TotalAmount_NonNegative", "[TotalAmount] >= 0");
        });
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).ValueGeneratedOnAdd();
        builder.Property(order => order.Status).HasConversion<int>();
        builder.Property(order => order.CreatedAtUtc).HasPrecision(3);
        builder.Property(order => order.UpdatedAtUtc).HasPrecision(3);
        builder.Property(order => order.ExpectedDate).HasColumnType("date");
        builder.Property(order => order.Notes).HasMaxLength(1000);
        builder.Property(order => order.TotalAmount).HasPrecision(18, 2);
        builder.Property(order => order.RowVersion).IsRowVersion();
        builder.HasIndex(order => new { order.StoreId, order.Status, order.CreatedAtUtc }).IsDescending(false, false, true);
        builder.HasIndex(order => new { order.CreatedAtUtc, order.StoreId, order.Status })
            .IsDescending(true, false, false)
            .IncludeProperties(order => order.TotalAmount);
        builder.HasIndex(order => new { order.SupplierId, order.CreatedAtUtc }).IsDescending(false, true);
        builder.HasIndex(order => new { order.Status, order.ExpectedDate });
        builder.HasOne(order => order.Store)
            .WithMany()
            .HasForeignKey(order => order.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.Supplier)
            .WithMany()
            .HasForeignKey(order => order.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(order => order.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(order => order.Items)
            .WithOne(item => item.PurchaseOrder)
            .HasForeignKey(item => item.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(order => order.Receipts)
            .WithOne(receipt => receipt.PurchaseOrder)
            .HasForeignKey(receipt => receipt.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(order => order.StatusHistory)
            .WithOne(history => history.PurchaseOrder)
            .HasForeignKey(history => history.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
