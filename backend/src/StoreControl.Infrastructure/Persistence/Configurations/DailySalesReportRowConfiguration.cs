using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Infrastructure.Persistence.Models;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class DailySalesReportRowConfiguration : IEntityTypeConfiguration<DailySalesReportRow>
{
    public void Configure(EntityTypeBuilder<DailySalesReportRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("vw_DailySales", "reporting");
        builder.Property(row => row.SaleDate).HasColumnType("date");
        builder.Property(row => row.GrossRevenue).HasPrecision(38, 2);
        builder.Property(row => row.DiscountAmount).HasPrecision(38, 2);
        builder.Property(row => row.Revenue).HasPrecision(38, 2);
    }
}
