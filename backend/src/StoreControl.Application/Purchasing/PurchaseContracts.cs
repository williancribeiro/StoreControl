using StoreControl.Application.Common;
using StoreControl.Domain.Purchasing;

namespace StoreControl.Application.Purchasing;

public sealed record PurchaseOrderListQuery(
    int Page,
    int PageSize,
    string? Search,
    int? StoreId,
    int? SupplierId,
    PurchaseOrderStatus? Status,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string SortBy,
    string SortDirection);

public sealed record PurchaseOrderSummary(
    long Id,
    string Number,
    int StoreId,
    string StoreCode,
    string StoreName,
    int SupplierId,
    string SupplierCode,
    string SupplierName,
    string Status,
    int ItemCount,
    decimal TotalAmount,
    DateTime CreatedAtUtc,
    DateOnly? ExpectedDate,
    string RowVersion);

public sealed record PurchaseOrderItemSummary(
    long Id,
    int ProductId,
    string ProductCode,
    string ProductName,
    decimal OrderedQuantity,
    decimal ReceivedQuantity,
    decimal RemainingQuantity,
    decimal UnitCost,
    decimal LineTotal);

public sealed record PurchaseReceiptItemSummary(
    long PurchaseOrderItemId,
    int ProductId,
    string ProductCode,
    string ProductName,
    decimal Quantity);

public sealed record PurchaseReceiptSummary(
    Guid Id,
    DateTime ReceivedAtUtc,
    Guid ReceivedByUserId,
    string ReceivedByUserName,
    string? Notes,
    IReadOnlyCollection<PurchaseReceiptItemSummary> Items);

public sealed record PurchaseStatusHistorySummary(
    long Id,
    string Status,
    DateTime ChangedAtUtc,
    Guid ChangedByUserId,
    string ChangedByUserName,
    string Description);

public sealed record PurchaseOrderDetails(
    long Id,
    string Number,
    int StoreId,
    string StoreCode,
    string StoreName,
    int SupplierId,
    string SupplierCode,
    string SupplierName,
    Guid CreatedByUserId,
    string CreatedByUserName,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateOnly? ExpectedDate,
    string? Notes,
    decimal TotalAmount,
    string RowVersion,
    IReadOnlyCollection<PurchaseOrderItemSummary> Items,
    IReadOnlyCollection<PurchaseReceiptSummary> Receipts,
    IReadOnlyCollection<PurchaseStatusHistorySummary> StatusHistory);

public sealed record CreatePurchaseOrderItemCommand(
    int ProductId,
    decimal Quantity,
    decimal UnitCost);

public sealed record CreatePurchaseOrderCommand(
    int StoreId,
    int SupplierId,
    DateOnly? ExpectedDate,
    string? Notes,
    IReadOnlyCollection<CreatePurchaseOrderItemCommand> Items);

public sealed record ReceivePurchaseItemCommand(long PurchaseOrderItemId, decimal Quantity);

public sealed record ReceivePurchaseCommand(
    Guid ReceiptId,
    string RowVersion,
    string? Notes,
    IReadOnlyCollection<ReceivePurchaseItemCommand> Items);

public sealed record PurchaseTransitionCommand(string RowVersion, string? Reason = null);

public sealed record ReceivePurchaseResult(
    PurchaseOrderDetails Purchase,
    Guid ReceiptId,
    bool WasAlreadyProcessed);

public interface IPurchaseService
{
    Task<PagedResult<PurchaseOrderSummary>> ListAsync(
        Guid userId,
        PurchaseOrderListQuery query,
        CancellationToken cancellationToken);

    Task<PurchaseOrderDetails> GetAsync(
        Guid userId,
        long purchaseOrderId,
        CancellationToken cancellationToken);

    Task<PurchaseOrderDetails> CreateAsync(
        Guid userId,
        CreatePurchaseOrderCommand command,
        CancellationToken cancellationToken);

    Task<PurchaseOrderDetails> SubmitAsync(
        Guid userId,
        long purchaseOrderId,
        PurchaseTransitionCommand command,
        CancellationToken cancellationToken);

    Task<ReceivePurchaseResult> ReceiveAsync(
        Guid userId,
        string correlationId,
        long purchaseOrderId,
        ReceivePurchaseCommand command,
        CancellationToken cancellationToken);

    Task<PurchaseOrderDetails> FinalizeAsync(
        Guid userId,
        long purchaseOrderId,
        PurchaseTransitionCommand command,
        CancellationToken cancellationToken);

    Task<PurchaseOrderDetails> CancelAsync(
        Guid userId,
        long purchaseOrderId,
        PurchaseTransitionCommand command,
        CancellationToken cancellationToken);
}
