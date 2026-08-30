using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Purchasing;
using StoreControl.Infrastructure.Identity;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.ToTable("PurchaseReceipts", "purchasing");
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id).ValueGeneratedNever();
        builder.Property(receipt => receipt.ReceivedAtUtc).HasPrecision(3);
        builder.Property(receipt => receipt.Notes).HasMaxLength(500);
        builder.HasIndex(receipt => new { receipt.PurchaseOrderId, receipt.ReceivedAtUtc }).IsDescending(false, true);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(receipt => receipt.ReceivedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(receipt => receipt.Items)
            .WithOne(item => item.Receipt)
            .HasForeignKey(item => item.PurchaseReceiptId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
