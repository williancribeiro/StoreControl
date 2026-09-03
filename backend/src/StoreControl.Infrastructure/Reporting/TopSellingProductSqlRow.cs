namespace StoreControl.Infrastructure.Reporting;

internal sealed class TopSellingProductSqlRow
{
    public int ProductId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public decimal QuantitySold { get; set; }

    public decimal GrossRevenue { get; set; }

    public long SaleCount { get; set; }
}
