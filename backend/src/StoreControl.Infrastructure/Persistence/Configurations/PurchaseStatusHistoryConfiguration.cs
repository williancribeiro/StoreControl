using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Purchasing;
using StoreControl.Infrastructure.Identity;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseStatusHistoryConfiguration : IEntityTypeConfiguration<PurchaseStatusHistory>
{
    public void Configure(EntityTypeBuilder<PurchaseStatusHistory> builder)
    {
        builder.ToTable("PurchaseStatusHistory", "purchasing", table =>
        {
            table.HasCheckConstraint("CK_PurchaseStatusHistory_Status", "[Status] BETWEEN 1 AND 6");
        });
        builder.HasKey(history => history.Id);
        builder.Property(history => history.Id).ValueGeneratedOnAdd();
        builder.Property(history => history.Status).HasConversion<int>();
        builder.Property(history => history.ChangedAtUtc).HasPrecision(3);
        builder.Property(history => history.Description).HasMaxLength(300);
        builder.HasIndex(history => new { history.PurchaseOrderId, history.ChangedAtUtc });
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(history => history.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
