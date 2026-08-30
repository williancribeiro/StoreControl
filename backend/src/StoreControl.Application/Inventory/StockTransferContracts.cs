using StoreControl.Application.Common;
using StoreControl.Domain.Inventory;

namespace StoreControl.Application.Inventory;

public sealed record StockTransferListQuery(
    int Page,
    int PageSize,
    string? Search,
    int? OriginStoreId,
    int? DestinationStoreId,
    StockTransferStatus? Status,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string SortBy,
    string SortDirection);

public sealed record StockTransferSummary(
    long Id,
    string Number,
    int OriginStoreId,
    string OriginStoreCode,
    string OriginStoreName,
    int DestinationStoreId,
    string DestinationStoreCode,
    string DestinationStoreName,
    string Status,
    int ItemCount,
    decimal TotalQuantity,
    DateTime CreatedAtUtc,
    DateTime? DispatchedAtUtc,
    DateTime? ReceivedAtUtc,
    string RowVersion);

public sealed record StockTransferItemSummary(
    long Id,
    int ProductId,
    string ProductCode,
    string ProductName,
    decimal Quantity);

public sealed record StockTransferHistorySummary(
    long Id,
    string Status,
    Guid ChangedByUserId,
    string ChangedByUserName,
    DateTime ChangedAtUtc,
    string Description);

public sealed record StockTransferDetails(
    long Id,
    string Number,
    Guid OperationId,
    int OriginStoreId,
    string OriginStoreCode,
    string OriginStoreName,
    int DestinationStoreId,
    string DestinationStoreCode,
    string DestinationStoreName,
    Guid CreatedByUserId,
    string CreatedByUserName,
    DateTime CreatedAtUtc,
    string Status,
    string? Notes,
    Guid? DispatchOperationId,
    Guid? DispatchedByUserId,
    string? DispatchedByUserName,
    DateTime? DispatchedAtUtc,
    Guid? ReceiptOperationId,
    Guid? ReceivedByUserId,
    string? ReceivedByUserName,
    DateTime? ReceivedAtUtc,
    Guid? CancellationOperationId,
    Guid? CancelledByUserId,
    string? CancelledByUserName,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    string RowVersion,
    IReadOnlyCollection<StockTransferItemSummary> Items,
    IReadOnlyCollection<StockTransferHistorySummary> StatusHistory);

public sealed record CreateStockTransferItemCommand(int ProductId, decimal Quantity);

public sealed record CreateStockTransferCommand(
    Guid OperationId,
    int OriginStoreId,
    int DestinationStoreId,
    string? Notes,
    IReadOnlyCollection<CreateStockTransferItemCommand> Items);

public sealed record StockTransferOperationCommand(Guid OperationId, string RowVersion);

public sealed record CancelStockTransferCommand(Guid OperationId, string RowVersion, string Reason);

public sealed record StockTransferOperationResult(
    StockTransferDetails Transfer,
    bool WasAlreadyProcessed);

public interface IStockTransferService
{
    Task<PagedResult<StockTransferSummary>> ListAsync(
        Guid userId,
        StockTransferListQuery query,
        CancellationToken cancellationToken);

    Task<StockTransferDetails> GetAsync(
        Guid userId,
        long stockTransferId,
        CancellationToken cancellationToken);

    Task<StockTransferOperationResult> CreateAsync(
        Guid userId,
        CreateStockTransferCommand command,
        CancellationToken cancellationToken);

    Task<StockTransferOperationResult> DispatchAsync(
        Guid userId,
        string correlationId,
        long stockTransferId,
        StockTransferOperationCommand command,
        CancellationToken cancellationToken);

    Task<StockTransferOperationResult> ReceiveAsync(
        Guid userId,
        string correlationId,
        long stockTransferId,
        StockTransferOperationCommand command,
        CancellationToken cancellationToken);

    Task<StockTransferOperationResult> CancelAsync(
        Guid userId,
        long stockTransferId,
        CancelStockTransferCommand command,
        CancellationToken cancellationToken);
}
