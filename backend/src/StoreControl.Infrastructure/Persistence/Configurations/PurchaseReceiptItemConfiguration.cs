using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Purchasing;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseReceiptItemConfiguration : IEntityTypeConfiguration<PurchaseReceiptItem>
{
    public void Configure(EntityTypeBuilder<PurchaseReceiptItem> builder)
    {
        builder.ToTable("PurchaseReceiptItems", "purchasing", table =>
        {
            table.HasCheckConstraint("CK_PurchaseReceiptItems_Quantity_Positive", "[Quantity] > 0");
        });
        builder.HasKey(item => new { item.PurchaseReceiptId, item.PurchaseOrderItemId });
        builder.Property(item => item.Quantity).HasPrecision(18, 3);
        builder.HasOne(item => item.PurchaseOrderItem)
            .WithMany()
            .HasForeignKey(item => item.PurchaseOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
