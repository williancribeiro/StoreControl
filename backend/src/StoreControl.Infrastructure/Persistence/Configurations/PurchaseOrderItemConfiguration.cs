using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Purchasing;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("PurchaseOrderItems", "purchasing", table =>
        {
            table.HasCheckConstraint("CK_PurchaseOrderItems_OrderedQuantity_Positive", "[OrderedQuantity] > 0");
            table.HasCheckConstraint(
                "CK_PurchaseOrderItems_ReceivedQuantity_Range",
                "[ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [OrderedQuantity]");
            table.HasCheckConstraint("CK_PurchaseOrderItems_UnitCost_NonNegative", "[UnitCost] >= 0");
            table.HasCheckConstraint("CK_PurchaseOrderItems_LineTotal_NonNegative", "[LineTotal] >= 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedOnAdd();
        builder.Property(item => item.ProductCode).HasMaxLength(30);
        builder.Property(item => item.ProductName).HasMaxLength(160);
        builder.Property(item => item.OrderedQuantity).HasPrecision(18, 3);
        builder.Property(item => item.ReceivedQuantity).HasPrecision(18, 3);
        builder.Property(item => item.UnitCost).HasPrecision(18, 4);
        builder.Property(item => item.LineTotal).HasPrecision(18, 2);
        builder.Ignore(item => item.RemainingQuantity);
        builder.HasIndex(item => new { item.PurchaseOrderId, item.ProductId }).IsUnique();
        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
