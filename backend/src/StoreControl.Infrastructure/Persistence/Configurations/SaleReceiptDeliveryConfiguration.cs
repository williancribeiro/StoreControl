using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Sales;
using StoreControl.Infrastructure.Messaging;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class SaleReceiptDeliveryConfiguration : IEntityTypeConfiguration<SaleReceiptDelivery>
{
    public void Configure(EntityTypeBuilder<SaleReceiptDelivery> builder)
    {
        builder.ToTable("SaleReceiptDeliveries", "integration", table =>
        {
            table.HasCheckConstraint("CK_SaleReceiptDeliveries_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint(
                "CK_SaleReceiptDeliveries_Recipient",
                "([Status] = 1 AND [RecipientHash] IS NOT NULL) OR ([Status] = 2 AND [RecipientHash] IS NULL)");
        });
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Id).ValueGeneratedOnAdd();
        builder.Property(delivery => delivery.Status).HasConversion<int>();
        builder.Property(delivery => delivery.RecipientHash).HasMaxLength(32).IsFixedLength();
        builder.Property(delivery => delivery.ProcessedAtUtc).HasPrecision(3);
        builder.HasIndex(delivery => delivery.MessageId).IsUnique();
        builder.HasIndex(delivery => delivery.SaleId).IsUnique();
        builder.HasOne<Sale>()
            .WithMany()
            .HasForeignKey(delivery => delivery.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
