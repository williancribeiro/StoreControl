using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Inventory;
using StoreControl.Infrastructure.Identity;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", "inventory", table =>
        {
            table.HasCheckConstraint("CK_StockMovements_QuantityChange_NonZero", "[QuantityChange] <> 0");
            table.HasCheckConstraint(
                "CK_StockMovements_BalanceEquation",
                "[QuantityAfter] = [QuantityBefore] + [QuantityChange]");
            table.HasCheckConstraint(
                "CK_StockMovements_Balances_NonNegative",
                "[QuantityBefore] >= 0 AND [QuantityAfter] >= 0");
            table.HasCheckConstraint(
                "CK_StockMovements_TypeDirection",
                "([MovementType] IN (1, 3, 5, 7, 8, 10) AND [QuantityChange] > 0) OR " +
                "([MovementType] IN (2, 4, 6, 9) AND [QuantityChange] < 0)");
            table.HasCheckConstraint(
                "CK_StockMovements_Reason_NotBlank",
                "LEN(LTRIM(RTRIM([Reason]))) > 0");
        });
        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.Id).ValueGeneratedOnAdd();
        builder.Property(movement => movement.Type).HasColumnName("MovementType").HasConversion<int>();
        builder.Property(movement => movement.QuantityChange).HasPrecision(18, 3);
        builder.Property(movement => movement.QuantityBefore).HasPrecision(18, 3);
        builder.Property(movement => movement.QuantityAfter).HasPrecision(18, 3);
        builder.Property(movement => movement.Reason).HasMaxLength(500);
        builder.Property(movement => movement.OccurredAtUtc).HasPrecision(3);
        builder.Property(movement => movement.CorrelationId).HasMaxLength(128);
        builder.Property(movement => movement.ReferenceType).HasMaxLength(50);
        builder.Property(movement => movement.ReferenceId).HasMaxLength(100);
        builder.HasIndex(movement => new { movement.StoreId, movement.OccurredAtUtc }).IsDescending(false, true);
        builder.HasIndex(movement => new { movement.ProductId, movement.OccurredAtUtc }).IsDescending(false, true);
        builder.HasIndex(movement => new { movement.OccurredAtUtc, movement.StoreId, movement.Type })
            .IsDescending(true, false, false)
            .IncludeProperties(movement => movement.QuantityChange);
        builder.HasIndex(movement => new { movement.ReferenceType, movement.ReferenceId })
            .HasFilter("[ReferenceType] IS NOT NULL");
        builder.HasIndex(movement => new
        {
            movement.StoreId,
            movement.ProductId,
            movement.ReferenceType,
            movement.ReferenceId
        })
            .IsUnique()
            .HasFilter("[ReferenceType] IS NOT NULL AND [ReferenceId] IS NOT NULL");
        builder.HasOne<StockBalance>()
            .WithMany()
            .HasForeignKey(movement => new { movement.StoreId, movement.ProductId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.Store)
            .WithMany()
            .HasForeignKey(movement => movement.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.Product)
            .WithMany()
            .HasForeignKey(movement => movement.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(movement => movement.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
