namespace StoreControl.Infrastructure.Persistence.Models;

public sealed class DailySalesReportRow
{
    public int StoreId { get; set; }

    public DateOnly SaleDate { get; set; }

    public long SaleCount { get; set; }

    public decimal GrossRevenue { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal Revenue { get; set; }
}
