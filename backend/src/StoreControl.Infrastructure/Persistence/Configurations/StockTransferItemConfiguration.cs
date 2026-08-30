using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Catalog;
using StoreControl.Domain.Inventory;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class StockTransferItemConfiguration : IEntityTypeConfiguration<StockTransferItem>
{
    public void Configure(EntityTypeBuilder<StockTransferItem> builder)
    {
        builder.ToTable("StockTransferItems", "inventory", table =>
        {
            table.HasCheckConstraint("CK_StockTransferItems_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_StockTransferItems_ProductCode_NotBlank", "LEN(LTRIM(RTRIM([ProductCode]))) > 0");
            table.HasCheckConstraint("CK_StockTransferItems_ProductName_NotBlank", "LEN(LTRIM(RTRIM([ProductName]))) > 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedOnAdd();
        builder.Property(item => item.ProductCode).HasMaxLength(50);
        builder.Property(item => item.ProductName).HasMaxLength(200);
        builder.Property(item => item.Quantity).HasPrecision(18, 3);
        builder.HasIndex(item => new { item.StockTransferId, item.ProductId }).IsUnique();
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
