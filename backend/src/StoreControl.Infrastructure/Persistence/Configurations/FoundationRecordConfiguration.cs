using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Infrastructure.Persistence.Models;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class FoundationRecordConfiguration : IEntityTypeConfiguration<FoundationRecord>
{
    public void Configure(EntityTypeBuilder<FoundationRecord> builder)
    {
        builder.ToTable("FoundationRecords", "platform");

        builder.HasKey(record => record.Key);

        builder.Property(record => record.Key)
            .HasMaxLength(100);

        builder.Property(record => record.Value)
            .HasMaxLength(500);

        builder.Property(record => record.UpdatedAtUtc)
            .HasPrecision(3);

        builder.HasData(new FoundationRecord
        {
            Key = "foundation-version",
            Value = "1",
            UpdatedAtUtc = new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
