using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Infrastructure.Persistence.Models;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class CacheRevisionConfiguration : IEntityTypeConfiguration<CacheRevision>
{
    public void Configure(EntityTypeBuilder<CacheRevision> builder)
    {
        builder.ToTable("CacheRevisions", "platform");
        builder.HasKey(revision => revision.Name);
        builder.Property(revision => revision.Name).HasMaxLength(40);
        builder.Property(revision => revision.Generation).HasDefaultValueSql("NEWID()");
    }
}
