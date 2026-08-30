using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Cash;
using StoreControl.Infrastructure.Identity;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.ToTable("CashMovements", "cash", table =>
        {
            table.HasCheckConstraint(
                "CK_CashMovements_TypeDirection",
                "([MovementType] IN (1, 2, 3) AND [Amount] > 0) OR " +
                "([MovementType] IN (4, 5) AND [Amount] < 0)");
            table.HasCheckConstraint(
                "CK_CashMovements_SalePayment",
                "([MovementType] = 2 AND [PaymentMethod] BETWEEN 1 AND 6 AND [SalePaymentId] IS NOT NULL AND [ReversedSalePaymentId] IS NULL) OR " +
                "([MovementType] = 5 AND [PaymentMethod] BETWEEN 1 AND 6 AND [SalePaymentId] IS NULL AND [ReversedSalePaymentId] IS NOT NULL) OR " +
                "([MovementType] NOT IN (2, 5) AND [PaymentMethod] IS NULL AND [SalePaymentId] IS NULL AND [ReversedSalePaymentId] IS NULL)");
            table.HasCheckConstraint(
                "CK_CashMovements_Reference",
                "([ReferenceType] IS NULL AND [ReferenceId] IS NULL) OR " +
                "([ReferenceType] IS NOT NULL AND [ReferenceId] IS NOT NULL)");
            table.HasCheckConstraint("CK_CashMovements_Reason_NotBlank", "LEN(LTRIM(RTRIM([Reason]))) > 0");
        });
        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.Id).ValueGeneratedOnAdd();
        builder.Property(movement => movement.Type).HasColumnName("MovementType").HasConversion<int>();
        builder.Property(movement => movement.PaymentMethod).HasConversion<int?>();
        builder.Property(movement => movement.Amount).HasPrecision(18, 2);
        builder.Property(movement => movement.Reason).HasMaxLength(300);
        builder.Property(movement => movement.OccurredAtUtc).HasPrecision(3);
        builder.Property(movement => movement.CorrelationId).HasMaxLength(128);
        builder.Property(movement => movement.ReferenceType).HasMaxLength(50);
        builder.Property(movement => movement.ReferenceId).HasMaxLength(100);
        builder.HasIndex(movement => new { movement.CashSessionId, movement.OccurredAtUtc });
        builder.HasIndex(movement => movement.OperationId).IsUnique().HasFilter("[OperationId] IS NOT NULL");
        builder.HasIndex(movement => movement.SalePaymentId).IsUnique().HasFilter("[SalePaymentId] IS NOT NULL");
        builder.HasIndex(movement => movement.ReversedSalePaymentId)
            .IsUnique()
            .HasFilter("[ReversedSalePaymentId] IS NOT NULL");
        builder.HasIndex(movement => new { movement.ReferenceType, movement.ReferenceId })
            .HasFilter("[ReferenceType] IS NOT NULL");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(movement => movement.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.SalePayment)
            .WithOne()
            .HasForeignKey<CashMovement>(movement => movement.SalePaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.ReversedSalePayment)
            .WithOne()
            .HasForeignKey<CashMovement>(movement => movement.ReversedSalePaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
