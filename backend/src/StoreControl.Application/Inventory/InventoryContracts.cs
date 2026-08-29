using StoreControl.Application.Common;
using StoreControl.Domain.Inventory;

namespace StoreControl.Application.Inventory;

public sealed record StockPositionSummary(
    int StoreId,
    string StoreCode,
    string StoreName,
    int ProductId,
    string ProductCode,
    string? ProductBarcode,
    string ProductName,
    bool ProductIsActive,
    decimal QuantityOnHand,
    decimal MinimumQuantity,
    bool IsBelowMinimum,
    string? RowVersion);

public sealed record StockPositionQuery(
    int Page,
    int PageSize,
    string? Search,
    int? StoreId,
    int? ProductId,
    bool? BelowMinimum,
    string SortBy,
    string SortDirection);

public sealed record StockMovementSummary(
    long Id,
    int StoreId,
    string StoreCode,
    string StoreName,
    int ProductId,
    string ProductCode,
    string ProductName,
    string Type,
    decimal QuantityChange,
    decimal QuantityBefore,
    decimal QuantityAfter,
    string Reason,
    Guid CreatedByUserId,
    string CreatedByUserName,
    DateTime OccurredAtUtc,
    string CorrelationId,
    string? ReferenceType,
    string? ReferenceId);

public sealed record StockMovementQuery(
    int Page,
    int PageSize,
    string? Search,
    int? StoreId,
    int? ProductId,
    StockMovementType? Type,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string SortBy,
    string SortDirection);

public sealed record CreateStockAdjustmentCommand(
    int StoreId,
    int ProductId,
    StockMovementType Type,
    decimal Quantity,
    string Reason,
    string? RowVersion);

public sealed record UpdateMinimumStockCommand(decimal MinimumQuantity, string? RowVersion);

public sealed record StockAdjustmentResult(
    StockPositionSummary Position,
    StockMovementSummary Movement);

public interface IInventoryService
{
    Task<PagedResult<StockPositionSummary>> ListPositionsAsync(
        Guid userId,
        StockPositionQuery query,
        CancellationToken cancellationToken);

    Task<StockPositionSummary> GetPositionAsync(
        Guid userId,
        int storeId,
        int productId,
        CancellationToken cancellationToken);

    Task<PagedResult<StockMovementSummary>> ListMovementsAsync(
        Guid userId,
        StockMovementQuery query,
        CancellationToken cancellationToken);

    Task<StockMovementSummary> GetMovementAsync(
        Guid userId,
        long movementId,
        CancellationToken cancellationToken);

    Task<StockAdjustmentResult> AdjustAsync(
        Guid userId,
        string correlationId,
        CreateStockAdjustmentCommand command,
        CancellationToken cancellationToken);

    Task<StockPositionSummary> UpdateMinimumAsync(
        Guid userId,
        int storeId,
        int productId,
        UpdateMinimumStockCommand command,
        CancellationToken cancellationToken);
}
