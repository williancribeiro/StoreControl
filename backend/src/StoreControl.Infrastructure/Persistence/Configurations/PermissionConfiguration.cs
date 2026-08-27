using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Infrastructure.Identity;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", "security");
        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.Code).HasMaxLength(100);
        builder.Property(permission => permission.Description).HasMaxLength(250);
        builder.HasIndex(permission => permission.Code).IsUnique();
    }
}
