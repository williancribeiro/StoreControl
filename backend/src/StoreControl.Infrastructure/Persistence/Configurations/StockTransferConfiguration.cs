using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Inventory;
using StoreControl.Infrastructure.Identity;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("StockTransfers", "inventory", table =>
        {
            table.HasCheckConstraint("CK_StockTransfers_DifferentStores", "[OriginStoreId] <> [DestinationStoreId]");
            table.HasCheckConstraint("CK_StockTransfers_Status", "[Status] BETWEEN 1 AND 4");
            table.HasCheckConstraint(
                "CK_StockTransfers_Lifecycle",
                "([Status] = 1 AND [DispatchOperationId] IS NULL AND [DispatchedByUserId] IS NULL AND [DispatchedAtUtc] IS NULL " +
                "AND [ReceiptOperationId] IS NULL AND [ReceivedByUserId] IS NULL AND [ReceivedAtUtc] IS NULL " +
                "AND [CancellationOperationId] IS NULL AND [CancelledByUserId] IS NULL AND [CancelledAtUtc] IS NULL AND [CancellationReason] IS NULL) OR " +
                "([Status] = 2 AND [DispatchOperationId] IS NOT NULL AND [DispatchedByUserId] IS NOT NULL AND [DispatchedAtUtc] IS NOT NULL " +
                "AND [ReceiptOperationId] IS NULL AND [ReceivedByUserId] IS NULL AND [ReceivedAtUtc] IS NULL " +
                "AND [CancellationOperationId] IS NULL AND [CancelledByUserId] IS NULL AND [CancelledAtUtc] IS NULL AND [CancellationReason] IS NULL) OR " +
                "([Status] = 3 AND [DispatchOperationId] IS NOT NULL AND [DispatchedByUserId] IS NOT NULL AND [DispatchedAtUtc] IS NOT NULL " +
                "AND [ReceiptOperationId] IS NOT NULL AND [ReceivedByUserId] IS NOT NULL AND [ReceivedAtUtc] IS NOT NULL " +
                "AND [CancellationOperationId] IS NULL AND [CancelledByUserId] IS NULL AND [CancelledAtUtc] IS NULL AND [CancellationReason] IS NULL) OR " +
                "([Status] = 4 AND [DispatchOperationId] IS NULL AND [DispatchedByUserId] IS NULL AND [DispatchedAtUtc] IS NULL " +
                "AND [ReceiptOperationId] IS NULL AND [ReceivedByUserId] IS NULL AND [ReceivedAtUtc] IS NULL " +
                "AND [CancellationOperationId] IS NOT NULL AND [CancelledByUserId] IS NOT NULL AND [CancelledAtUtc] IS NOT NULL " +
                "AND LEN(LTRIM(RTRIM([CancellationReason]))) > 0)");
        });
        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.Id).ValueGeneratedOnAdd();
        builder.Property(transfer => transfer.Status).HasConversion<int>();
        builder.Property(transfer => transfer.Notes).HasMaxLength(1000);
        builder.Property(transfer => transfer.CancellationReason).HasMaxLength(500);
        builder.Property(transfer => transfer.CreatedAtUtc).HasPrecision(3);
        builder.Property(transfer => transfer.DispatchedAtUtc).HasPrecision(3);
        builder.Property(transfer => transfer.ReceivedAtUtc).HasPrecision(3);
        builder.Property(transfer => transfer.CancelledAtUtc).HasPrecision(3);
        builder.Property(transfer => transfer.RowVersion).IsRowVersion();
        builder.HasIndex(transfer => transfer.OperationId).IsUnique();
        builder.HasIndex(transfer => transfer.DispatchOperationId)
            .IsUnique()
            .HasFilter("[DispatchOperationId] IS NOT NULL");
        builder.HasIndex(transfer => transfer.ReceiptOperationId)
            .IsUnique()
            .HasFilter("[ReceiptOperationId] IS NOT NULL");
        builder.HasIndex(transfer => transfer.CancellationOperationId)
            .IsUnique()
            .HasFilter("[CancellationOperationId] IS NOT NULL");
        builder.HasIndex(transfer => new { transfer.OriginStoreId, transfer.CreatedAtUtc }).IsDescending(false, true);
        builder.HasIndex(transfer => new { transfer.DestinationStoreId, transfer.CreatedAtUtc }).IsDescending(false, true);
        builder.HasIndex(transfer => new { transfer.Status, transfer.CreatedAtUtc }).IsDescending(false, true);
        builder.HasOne(transfer => transfer.OriginStore)
            .WithMany()
            .HasForeignKey(transfer => transfer.OriginStoreId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transfer => transfer.DestinationStore)
            .WithMany()
            .HasForeignKey(transfer => transfer.DestinationStoreId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(transfer => transfer.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(transfer => transfer.DispatchedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(transfer => transfer.ReceivedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(transfer => transfer.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(transfer => transfer.Items)
            .WithOne(item => item.Transfer)
            .HasForeignKey(item => item.StockTransferId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(transfer => transfer.StatusHistory)
            .WithOne(history => history.Transfer)
            .HasForeignKey(history => history.StockTransferId)
            .OnDelete(DeleteBehavior.Restrict);
    }

}
