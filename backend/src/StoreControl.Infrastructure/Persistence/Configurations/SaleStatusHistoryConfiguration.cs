using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Sales;
using StoreControl.Infrastructure.Identity;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class SaleStatusHistoryConfiguration : IEntityTypeConfiguration<SaleStatusHistory>
{
    public void Configure(EntityTypeBuilder<SaleStatusHistory> builder)
    {
        builder.ToTable("SaleStatusHistory", "sales", table =>
        {
            table.HasCheckConstraint("CK_SaleStatusHistory_Status", "[Status] IN (2, 3)");
            table.HasCheckConstraint(
                "CK_SaleStatusHistory_Description_NotBlank",
                "LEN(LTRIM(RTRIM([Description]))) > 0");
        });
        builder.HasKey(history => history.Id);
        builder.Property(history => history.Id).ValueGeneratedOnAdd();
        builder.Property(history => history.Status).HasConversion<int>();
        builder.Property(history => history.ChangedAtUtc).HasPrecision(3);
        builder.Property(history => history.Description).HasMaxLength(500);
        builder.HasIndex(history => new { history.SaleId, history.ChangedAtUtc });
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(history => history.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
