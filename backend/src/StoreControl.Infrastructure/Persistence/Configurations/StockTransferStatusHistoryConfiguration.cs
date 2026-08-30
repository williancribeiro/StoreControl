using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Inventory;
using StoreControl.Infrastructure.Identity;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class StockTransferStatusHistoryConfiguration : IEntityTypeConfiguration<StockTransferStatusHistory>
{
    public void Configure(EntityTypeBuilder<StockTransferStatusHistory> builder)
    {
        builder.ToTable("StockTransferStatusHistory", "inventory", table =>
        {
            table.HasCheckConstraint("CK_StockTransferStatusHistory_Status", "[Status] BETWEEN 1 AND 4");
            table.HasCheckConstraint(
                "CK_StockTransferStatusHistory_Description_NotBlank",
                "LEN(LTRIM(RTRIM([Description]))) > 0");
        });
        builder.HasKey(history => history.Id);
        builder.Property(history => history.Id).ValueGeneratedOnAdd();
        builder.Property(history => history.Status).HasConversion<int>();
        builder.Property(history => history.ChangedAtUtc).HasPrecision(3);
        builder.Property(history => history.Description).HasMaxLength(500);
        builder.HasIndex(history => new { history.StockTransferId, history.ChangedAtUtc });
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(history => history.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
