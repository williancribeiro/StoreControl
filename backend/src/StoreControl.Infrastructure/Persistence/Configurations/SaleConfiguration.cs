using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Cash;
using StoreControl.Domain.Sales;
using StoreControl.Infrastructure.Identity;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales", "sales", table =>
        {
            table.HasCheckConstraint("CK_Sales_Status_Finalized", "[Status] = 2");
            table.HasCheckConstraint("CK_Sales_Subtotal_Positive", "[Subtotal] > 0");
            table.HasCheckConstraint(
                "CK_Sales_Discount_Range",
                "[DiscountAmount] >= 0 AND [DiscountAmount] < [Subtotal]");
            table.HasCheckConstraint(
                "CK_Sales_Total_Equation",
                "[TotalAmount] = [Subtotal] - [DiscountAmount] AND [TotalAmount] > 0");
        });
        builder.HasKey(sale => sale.Id);
        builder.Property(sale => sale.Id).ValueGeneratedOnAdd();
        builder.Property(sale => sale.Status).HasConversion<int>();
        builder.Property(sale => sale.CreatedAtUtc).HasPrecision(3);
        builder.Property(sale => sale.FinalizedAtUtc).HasPrecision(3).IsRequired();
        builder.Property(sale => sale.Subtotal).HasPrecision(18, 2);
        builder.Property(sale => sale.DiscountAmount).HasPrecision(18, 2);
        builder.Property(sale => sale.TotalAmount).HasPrecision(18, 2);
        builder.HasIndex(sale => sale.OperationId).IsUnique();
        builder.HasIndex(sale => new { sale.StoreId, sale.FinalizedAtUtc }).IsDescending(false, true);
        builder.HasIndex(sale => new { sale.CustomerId, sale.FinalizedAtUtc }).IsDescending(false, true);
        builder.HasIndex(sale => sale.CashSessionId);
        builder.HasOne(sale => sale.Store)
            .WithMany()
            .HasForeignKey(sale => sale.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(sale => sale.Customer)
            .WithMany()
            .HasForeignKey(sale => sale.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashSession>()
            .WithMany()
            .HasForeignKey(sale => sale.CashSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(sale => sale.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(sale => sale.Items)
            .WithOne(item => item.Sale)
            .HasForeignKey(item => item.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(sale => sale.Payments)
            .WithOne(payment => payment.Sale)
            .HasForeignKey(payment => payment.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
