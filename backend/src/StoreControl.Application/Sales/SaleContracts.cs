using StoreControl.Application.Common;
using StoreControl.Domain.Sales;

namespace StoreControl.Application.Sales;

public sealed record SaleListQuery(
    int Page,
    int PageSize,
    string? Search,
    int? StoreId,
    int? CustomerId,
    PaymentMethod? PaymentMethod,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string SortBy,
    string SortDirection);

public sealed record SaleSummary(
    long Id,
    string Number,
    Guid OperationId,
    int StoreId,
    string StoreCode,
    string StoreName,
    int? CustomerId,
    string? CustomerCode,
    string? CustomerName,
    Guid CreatedByUserId,
    string CreatedByUserName,
    long CashSessionId,
    string Status,
    int ItemCount,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    DateTime FinalizedAtUtc);

public sealed record SaleItemSummary(
    long Id,
    int ProductId,
    string ProductCode,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineSubtotal);

public sealed record SalePaymentSummary(
    long Id,
    string Method,
    decimal Amount,
    string? Reference);

public sealed record SaleStatusHistorySummary(
    long Id,
    string Status,
    Guid ChangedByUserId,
    string ChangedByUserName,
    DateTime ChangedAtUtc,
    string Description);

public sealed record SaleDetails(
    long Id,
    string Number,
    Guid OperationId,
    int StoreId,
    string StoreCode,
    string StoreName,
    int? CustomerId,
    string? CustomerCode,
    string? CustomerName,
    Guid CreatedByUserId,
    string CreatedByUserName,
    long CashSessionId,
    string CashRegisterCode,
    string Status,
    DateTime CreatedAtUtc,
    DateTime FinalizedAtUtc,
    Guid? CancellationOperationId,
    long? CancellationCashSessionId,
    string? CancellationCashRegisterCode,
    Guid? CancelledByUserId,
    string? CancelledByUserName,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    string RowVersion,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    IReadOnlyCollection<SaleItemSummary> Items,
    IReadOnlyCollection<SalePaymentSummary> Payments,
    IReadOnlyCollection<SaleStatusHistorySummary> StatusHistory);

public sealed record FinalizeSaleItemCommand(int ProductId, decimal Quantity);

public sealed record FinalizeSalePaymentCommand(PaymentMethod Method, decimal Amount, string? Reference);

public sealed record FinalizeSaleCommand(
    Guid OperationId,
    int StoreId,
    long CashSessionId,
    int? CustomerId,
    decimal DiscountAmount,
    IReadOnlyCollection<FinalizeSaleItemCommand> Items,
    IReadOnlyCollection<FinalizeSalePaymentCommand> Payments);

public sealed record FinalizeSaleResult(SaleDetails Sale, bool WasAlreadyProcessed);

public sealed record CancelSaleCommand(
    Guid OperationId,
    long CashSessionId,
    string RowVersion,
    string Reason);

public sealed record CancelSaleResult(SaleDetails Sale, bool WasAlreadyProcessed);

public interface ISaleService
{
    Task<PagedResult<SaleSummary>> ListAsync(
        Guid userId,
        SaleListQuery query,
        CancellationToken cancellationToken);

    Task<SaleDetails> GetAsync(Guid userId, long saleId, CancellationToken cancellationToken);

    Task<FinalizeSaleResult> FinalizeAsync(
        Guid userId,
        string correlationId,
        FinalizeSaleCommand command,
        CancellationToken cancellationToken);

    Task<CancelSaleResult> CancelAsync(
        Guid userId,
        string correlationId,
        long saleId,
        CancelSaleCommand command,
        CancellationToken cancellationToken);
}
