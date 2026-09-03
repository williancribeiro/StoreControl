using StoreControl.Application.Common;

namespace StoreControl.Application.Reporting;

public sealed record DashboardQuery(int? StoreId, DateOnly? ReferenceDate);

public sealed record TopSellingProductSummary(
    int ProductId,
    string ProductCode,
    string ProductName,
    decimal QuantitySold,
    decimal GrossRevenue,
    long SaleCount);

public sealed record LowStockSummary(
    int StoreId,
    string StoreCode,
    string StoreName,
    int ProductId,
    string ProductCode,
    string ProductName,
    decimal QuantityOnHand,
    decimal MinimumQuantity);

public sealed record SalesEvolutionPoint(DateOnly Date, long SaleCount, decimal Revenue);

public sealed record DashboardSummary(
    DateOnly ReferenceDate,
    int? StoreId,
    long SalesCount,
    decimal Revenue,
    decimal AverageTicket,
    int LowStockCount,
    IReadOnlyCollection<TopSellingProductSummary> TopSellingProducts,
    IReadOnlyCollection<LowStockSummary> LowStockItems,
    IReadOnlyCollection<SalesEvolutionPoint> SalesEvolution);

public sealed record SalesReportQuery(DateOnly FromDate, DateOnly ToDate, int? StoreId);

public sealed record SalesByStoreSummary(
    int StoreId,
    string StoreCode,
    string StoreName,
    long SaleCount,
    decimal Revenue,
    decimal AverageTicket);

public sealed record SalesReport(
    DateOnly FromDate,
    DateOnly ToDate,
    int? StoreId,
    long SaleCount,
    decimal GrossRevenue,
    decimal DiscountAmount,
    decimal Revenue,
    decimal AverageTicket,
    IReadOnlyCollection<SalesEvolutionPoint> DailyEvolution,
    IReadOnlyCollection<SalesByStoreSummary> ByStore,
    IReadOnlyCollection<TopSellingProductSummary> TopSellingProducts);

public sealed record InventoryReportQuery(
    int Page,
    int PageSize,
    string? Search,
    int? StoreId,
    bool? BelowMinimum,
    string SortBy,
    string SortDirection);

public sealed record InventoryReportRow(
    int StoreId,
    string StoreCode,
    string StoreName,
    int ProductId,
    string ProductCode,
    string ProductName,
    string CategoryName,
    decimal QuantityOnHand,
    decimal MinimumQuantity,
    bool IsBelowMinimum);

public sealed record MovementReportQuery(DateOnly FromDate, DateOnly ToDate, int? StoreId);

public sealed record MovementTypeSummary(
    string Type,
    long MovementCount,
    decimal EntryQuantity,
    decimal ExitQuantity);

public sealed record MovementStoreSummary(
    int StoreId,
    string StoreCode,
    string StoreName,
    long MovementCount,
    decimal EntryQuantity,
    decimal ExitQuantity);

public sealed record MovementReport(
    DateOnly FromDate,
    DateOnly ToDate,
    int? StoreId,
    long MovementCount,
    decimal EntryQuantity,
    decimal ExitQuantity,
    IReadOnlyCollection<MovementTypeSummary> ByType,
    IReadOnlyCollection<MovementStoreSummary> ByStore);

public sealed record PurchaseReportQuery(DateOnly FromDate, DateOnly ToDate, int? StoreId);

public sealed record PurchaseStatusSummary(string Status, long OrderCount, decimal OrderedAmount);

public sealed record PurchaseStoreSummary(
    int StoreId,
    string StoreCode,
    string StoreName,
    long OrderCount,
    decimal OrderedAmount,
    decimal ReceivedAmount);

public sealed record PurchaseSupplierSummary(
    int SupplierId,
    string SupplierCode,
    string SupplierName,
    long OrderCount,
    decimal OrderedAmount);

public sealed record PurchaseReport(
    DateOnly FromDate,
    DateOnly ToDate,
    int? StoreId,
    long OrderCount,
    decimal OrderedAmount,
    decimal ReceivedAmount,
    IReadOnlyCollection<PurchaseStatusSummary> ByStatus,
    IReadOnlyCollection<PurchaseStoreSummary> ByStore,
    IReadOnlyCollection<PurchaseSupplierSummary> BySupplier);

public interface IReportingService
{
    Task<DashboardSummary> GetDashboardAsync(
        Guid userId,
        DashboardQuery query,
        CancellationToken cancellationToken);

    Task<SalesReport> GetSalesAsync(
        Guid userId,
        SalesReportQuery query,
        CancellationToken cancellationToken);

    Task<PagedResult<InventoryReportRow>> GetInventoryAsync(
        Guid userId,
        InventoryReportQuery query,
        CancellationToken cancellationToken);

    Task<MovementReport> GetMovementsAsync(
        Guid userId,
        MovementReportQuery query,
        CancellationToken cancellationToken);

    Task<PurchaseReport> GetPurchasesAsync(
        Guid userId,
        PurchaseReportQuery query,
        CancellationToken cancellationToken);
}
