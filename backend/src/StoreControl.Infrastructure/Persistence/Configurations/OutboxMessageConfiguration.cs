using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Infrastructure.Messaging;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", "integration", table =>
        {
            table.HasCheckConstraint("CK_OutboxMessages_Payload_Json", "ISJSON([Payload]) = 1");
            table.HasCheckConstraint("CK_OutboxMessages_PublishAttempts", "[PublishAttempts] >= 0");
            table.HasCheckConstraint(
                "CK_OutboxMessages_Lease",
                "([LockId] IS NULL AND [LockedUntilUtc] IS NULL) OR ([LockId] IS NOT NULL AND [LockedUntilUtc] IS NOT NULL)");
        });
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Type).HasMaxLength(120);
        builder.Property(message => message.RoutingKey).HasMaxLength(120);
        builder.Property(message => message.Payload).HasColumnType("nvarchar(max)");
        builder.Property(message => message.CorrelationId).HasMaxLength(100);
        builder.Property(message => message.TraceParent).HasMaxLength(55);
        builder.Property(message => message.TraceState).HasMaxLength(512);
        builder.Property(message => message.OccurredAtUtc).HasPrecision(3);
        builder.Property(message => message.CreatedAtUtc).HasPrecision(3);
        builder.Property(message => message.NextAttemptAtUtc).HasPrecision(3);
        builder.Property(message => message.PublishedAtUtc).HasPrecision(3);
        builder.Property(message => message.LockedUntilUtc).HasPrecision(3);
        builder.Property(message => message.LastError).HasMaxLength(1000);
        builder.HasIndex(message => new { message.PublishedAtUtc, message.NextAttemptAtUtc, message.LockedUntilUtc })
            .HasFilter("[PublishedAtUtc] IS NULL")
            .IncludeProperties(message => new { message.CreatedAtUtc, message.PublishAttempts });
    }
}
