namespace StoreControl.Infrastructure.Persistence.Models;

public sealed class StockPositionReportRow
{
    public int StoreId { get; set; }

    public string StoreCode { get; set; } = string.Empty;

    public string StoreName { get; set; } = string.Empty;

    public int ProductId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public decimal QuantityOnHand { get; set; }

    public decimal MinimumQuantity { get; set; }

    public bool IsBelowMinimum { get; set; }
}
