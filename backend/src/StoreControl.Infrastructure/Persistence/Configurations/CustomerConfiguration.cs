using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Parties;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", "parties");
        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.Code).HasMaxLength(30);
        builder.Property(customer => customer.Name).HasMaxLength(160);
        builder.Property(customer => customer.Document).HasMaxLength(14);
        builder.Property(customer => customer.Email).HasMaxLength(254);
        builder.Property(customer => customer.Phone).HasMaxLength(30);
        builder.Property(customer => customer.CreatedAtUtc).HasPrecision(3);
        builder.Property(customer => customer.UpdatedAtUtc).HasPrecision(3);
        builder.Property(customer => customer.RowVersion).IsRowVersion();
        builder.HasIndex(customer => customer.Code).IsUnique();
        builder.HasIndex(customer => customer.Document).IsUnique().HasFilter("[Document] IS NOT NULL");
        builder.HasIndex(customer => new { customer.IsActive, customer.Name });
    }
}
