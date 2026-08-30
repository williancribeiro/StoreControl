using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Sales;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems", "sales", table =>
        {
            table.HasCheckConstraint("CK_SaleItems_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_SaleItems_UnitPrice_NonNegative", "[UnitPrice] >= 0");
            table.HasCheckConstraint("CK_SaleItems_LineSubtotal_NonNegative", "[LineSubtotal] >= 0");
            table.HasCheckConstraint("CK_SaleItems_ProductCode_NotBlank", "LEN(LTRIM(RTRIM([ProductCode]))) > 0");
            table.HasCheckConstraint("CK_SaleItems_ProductName_NotBlank", "LEN(LTRIM(RTRIM([ProductName]))) > 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedOnAdd();
        builder.Property(item => item.ProductCode).HasMaxLength(30);
        builder.Property(item => item.ProductName).HasMaxLength(160);
        builder.Property(item => item.Quantity).HasPrecision(18, 3);
        builder.Property(item => item.UnitPrice).HasPrecision(18, 2);
        builder.Property(item => item.LineSubtotal).HasPrecision(18, 2);
        builder.HasIndex(item => new { item.SaleId, item.ProductId }).IsUnique();
        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
