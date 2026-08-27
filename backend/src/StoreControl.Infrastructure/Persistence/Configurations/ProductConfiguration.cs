using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Catalog;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", "catalog", table =>
        {
            table.HasCheckConstraint("CK_Products_CostPrice_NonNegative", "[CostPrice] >= 0");
            table.HasCheckConstraint("CK_Products_SalePrice_NonNegative", "[SalePrice] >= 0");
        });
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Code).HasMaxLength(30);
        builder.Property(product => product.Barcode).HasMaxLength(14);
        builder.Property(product => product.Name).HasMaxLength(160);
        builder.Property(product => product.Description).HasMaxLength(1000);
        builder.Property(product => product.CostPrice).HasPrecision(18, 4);
        builder.Property(product => product.SalePrice).HasPrecision(18, 2);
        builder.Property(product => product.CreatedAtUtc).HasPrecision(3);
        builder.Property(product => product.UpdatedAtUtc).HasPrecision(3);
        builder.Property(product => product.RowVersion).IsRowVersion();
        builder.HasIndex(product => product.Code).IsUnique();
        builder.HasIndex(product => product.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
        builder.HasIndex(product => new { product.CategoryId, product.IsActive, product.Name });
        builder.HasIndex(product => new { product.IsActive, product.Name });
        builder.HasOne(product => product.Category)
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
