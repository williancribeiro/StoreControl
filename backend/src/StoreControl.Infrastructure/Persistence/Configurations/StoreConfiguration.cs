using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Stores;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("Stores", "organization");
        builder.HasKey(store => store.Id);
        builder.Property(store => store.Code).HasMaxLength(20);
        builder.Property(store => store.Name).HasMaxLength(120);
        builder.Property(store => store.CreatedAtUtc).HasPrecision(3);
        builder.Property(store => store.UpdatedAtUtc).HasPrecision(3);
        builder.Property(store => store.RowVersion).IsRowVersion();
        builder.HasIndex(store => store.Code).IsUnique();
        builder.HasIndex(store => new { store.IsActive, store.Name });
    }
}
