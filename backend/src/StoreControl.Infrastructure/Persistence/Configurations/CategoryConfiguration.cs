using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Catalog;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", "catalog");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Code).HasMaxLength(30);
        builder.Property(category => category.Name).HasMaxLength(120);
        builder.Property(category => category.Description).HasMaxLength(500);
        builder.Property(category => category.CreatedAtUtc).HasPrecision(3);
        builder.Property(category => category.UpdatedAtUtc).HasPrecision(3);
        builder.Property(category => category.RowVersion).IsRowVersion();
        builder.HasIndex(category => category.Code).IsUnique();
        builder.HasIndex(category => new { category.IsActive, category.Name });
    }
}
