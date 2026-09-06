using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Infrastructure.Messaging;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages", "integration");
        builder.HasKey(message => new { message.Consumer, message.MessageId });
        builder.Property(message => message.Consumer).HasMaxLength(120);
        builder.Property(message => message.PayloadHash).HasMaxLength(32).IsFixedLength();
        builder.Property(message => message.ProcessedAtUtc).HasPrecision(3);
        builder.HasIndex(message => message.ProcessedAtUtc);
    }
}
