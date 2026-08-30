using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Sales;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> builder)
    {
        builder.ToTable("SalePayments", "sales", table =>
        {
            table.HasCheckConstraint("CK_SalePayments_Method", "[PaymentMethod] BETWEEN 1 AND 6");
            table.HasCheckConstraint("CK_SalePayments_Amount_Positive", "[Amount] > 0");
        });
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).ValueGeneratedOnAdd();
        builder.Property(payment => payment.Method).HasColumnName("PaymentMethod").HasConversion<int>();
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.Reference).HasMaxLength(100);
        builder.HasIndex(payment => new { payment.SaleId, payment.Method });
    }
}
