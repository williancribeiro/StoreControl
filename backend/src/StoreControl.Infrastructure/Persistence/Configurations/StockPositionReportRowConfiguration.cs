using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Infrastructure.Persistence.Models;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class StockPositionReportRowConfiguration : IEntityTypeConfiguration<StockPositionReportRow>
{
    public void Configure(EntityTypeBuilder<StockPositionReportRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_StockPosition", "reporting");
        builder.Property(row => row.StoreCode).HasMaxLength(20);
        builder.Property(row => row.StoreName).HasMaxLength(120);
        builder.Property(row => row.ProductCode).HasMaxLength(30);
        builder.Property(row => row.ProductName).HasMaxLength(160);
        builder.Property(row => row.CategoryName).HasMaxLength(120);
        builder.Property(row => row.QuantityOnHand).HasPrecision(18, 3);
        builder.Property(row => row.MinimumQuantity).HasPrecision(18, 3);
    }
}
