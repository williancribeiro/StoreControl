using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Parties;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers", "parties");
        builder.HasKey(supplier => supplier.Id);
        builder.Property(supplier => supplier.Code).HasMaxLength(30);
        builder.Property(supplier => supplier.Name).HasMaxLength(160);
        builder.Property(supplier => supplier.Document).HasMaxLength(14);
        builder.Property(supplier => supplier.Email).HasMaxLength(254);
        builder.Property(supplier => supplier.Phone).HasMaxLength(30);
        builder.Property(supplier => supplier.CreatedAtUtc).HasPrecision(3);
        builder.Property(supplier => supplier.UpdatedAtUtc).HasPrecision(3);
        builder.Property(supplier => supplier.RowVersion).IsRowVersion();
        builder.HasIndex(supplier => supplier.Code).IsUnique();
        builder.HasIndex(supplier => supplier.Document).IsUnique().HasFilter("[Document] IS NOT NULL");
        builder.HasIndex(supplier => new { supplier.IsActive, supplier.Name });
    }
}
