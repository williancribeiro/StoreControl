using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Infrastructure.Identity;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class UserStoreRoleConfiguration : IEntityTypeConfiguration<UserStoreRole>
{
    public void Configure(EntityTypeBuilder<UserStoreRole> builder)
    {
        builder.ToTable("UserStoreRoles", "security");
        builder.HasKey(assignment => assignment.Id);
        builder.HasIndex(assignment => new { assignment.UserId, assignment.StoreId }).IsUnique();
        builder.HasIndex(assignment => new { assignment.StoreId, assignment.RoleId });
        builder.HasOne(assignment => assignment.User)
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(assignment => assignment.Role)
            .WithMany()
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.Store)
            .WithMany()
            .HasForeignKey(assignment => assignment.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
