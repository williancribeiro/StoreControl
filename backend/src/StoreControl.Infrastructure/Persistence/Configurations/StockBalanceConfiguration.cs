using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Inventory;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("StockBalances", "inventory", table =>
        {
            table.HasCheckConstraint("CK_StockBalances_QuantityOnHand_NonNegative", "[QuantityOnHand] >= 0");
            table.HasCheckConstraint("CK_StockBalances_MinimumQuantity_NonNegative", "[MinimumQuantity] >= 0");
        });
        builder.HasKey(balance => new { balance.StoreId, balance.ProductId });
        builder.Property(balance => balance.QuantityOnHand).HasPrecision(18, 3);
        builder.Property(balance => balance.MinimumQuantity).HasPrecision(18, 3);
        builder.Property(balance => balance.CreatedAtUtc).HasPrecision(3);
        builder.Property(balance => balance.UpdatedAtUtc).HasPrecision(3);
        builder.Property(balance => balance.RowVersion).IsRowVersion();
        builder.HasIndex(balance => new { balance.ProductId, balance.StoreId });
        builder.HasIndex(balance => new { balance.StoreId, balance.QuantityOnHand, balance.MinimumQuantity });
        builder.HasOne(balance => balance.Store)
            .WithMany()
            .HasForeignKey(balance => balance.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(balance => balance.Product)
            .WithMany()
            .HasForeignKey(balance => balance.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
