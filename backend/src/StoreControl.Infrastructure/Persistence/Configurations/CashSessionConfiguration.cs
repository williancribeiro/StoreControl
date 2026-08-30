using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Cash;
using StoreControl.Infrastructure.Identity;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> builder)
    {
        builder.ToTable("CashSessions", "cash", table =>
        {
            table.HasCheckConstraint("CK_CashSessions_Status", "[Status] BETWEEN 1 AND 2");
            table.HasCheckConstraint(
                "CK_CashSessions_Amounts_NonNegative",
                "[OpeningAmount] >= 0 AND [ExpectedCashAmount] >= 0 AND " +
                "([DeclaredClosingAmount] IS NULL OR [DeclaredClosingAmount] >= 0)");
            table.HasCheckConstraint(
                "CK_CashSessions_ClosingState",
                "([Status] = 1 AND [ClosedByUserId] IS NULL AND [ClosedAtUtc] IS NULL AND " +
                "[DeclaredClosingAmount] IS NULL AND [DifferenceAmount] IS NULL) OR " +
                "([Status] = 2 AND [ClosedByUserId] IS NOT NULL AND [ClosedAtUtc] IS NOT NULL AND " +
                "[DeclaredClosingAmount] IS NOT NULL AND [DifferenceAmount] IS NOT NULL)");
        });
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedOnAdd();
        builder.Property(session => session.Status).HasConversion<int>();
        builder.Property(session => session.OpenedAtUtc).HasPrecision(3);
        builder.Property(session => session.ClosedAtUtc).HasPrecision(3);
        builder.Property(session => session.UpdatedAtUtc).HasPrecision(3);
        builder.Property(session => session.OpeningAmount).HasPrecision(18, 2);
        builder.Property(session => session.ExpectedCashAmount).HasPrecision(18, 2);
        builder.Property(session => session.DeclaredClosingAmount).HasPrecision(18, 2);
        builder.Property(session => session.DifferenceAmount).HasPrecision(18, 2);
        builder.Property(session => session.RowVersion).IsRowVersion();
        builder.HasIndex(session => session.OperationId).IsUnique();
        builder.HasIndex(session => new { session.CashRegisterId, session.Status })
            .IsUnique()
            .HasFilter("[Status] = 1");
        builder.HasIndex(session => session.OpenedAtUtc).IsDescending();
        builder.HasOne(session => session.CashRegister)
            .WithMany()
            .HasForeignKey(session => session.CashRegisterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(session => session.OpenedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(session => session.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(session => session.Movements)
            .WithOne(movement => movement.Session)
            .HasForeignKey(movement => movement.CashSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
