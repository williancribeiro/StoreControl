using StoreControl.Application.Common;
using StoreControl.Domain.Cash;

namespace StoreControl.Application.Cash;

public sealed record CashRegisterSummary(
    int Id,
    int StoreId,
    string StoreCode,
    string StoreName,
    string Code,
    string Name,
    bool IsActive);

public sealed record CashSessionListQuery(
    int Page,
    int PageSize,
    int? StoreId,
    int? CashRegisterId,
    CashSessionStatus? Status,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string SortBy,
    string SortDirection);

public sealed record CashSessionSummary(
    long Id,
    int CashRegisterId,
    string CashRegisterCode,
    int StoreId,
    string StoreCode,
    string StoreName,
    string Status,
    DateTime OpenedAtUtc,
    DateTime? ClosedAtUtc,
    string OpenedByUserName,
    decimal OpeningAmount,
    decimal ExpectedCashAmount,
    decimal? DeclaredClosingAmount,
    decimal? DifferenceAmount,
    string RowVersion);

public sealed record CashMovementSummary(
    long Id,
    string Type,
    string? PaymentMethod,
    decimal Amount,
    string Reason,
    Guid CreatedByUserId,
    string CreatedByUserName,
    DateTime OccurredAtUtc,
    string CorrelationId,
    string? ReferenceType,
    string? ReferenceId);

public sealed record CashSessionDetails(
    long Id,
    Guid OperationId,
    int CashRegisterId,
    string CashRegisterCode,
    string CashRegisterName,
    int StoreId,
    string StoreCode,
    string StoreName,
    string Status,
    Guid OpenedByUserId,
    string OpenedByUserName,
    DateTime OpenedAtUtc,
    Guid? ClosedByUserId,
    string? ClosedByUserName,
    DateTime? ClosedAtUtc,
    decimal OpeningAmount,
    decimal ExpectedCashAmount,
    decimal? DeclaredClosingAmount,
    decimal? DifferenceAmount,
    string RowVersion,
    IReadOnlyCollection<CashMovementSummary> Movements);

public sealed record OpenCashSessionCommand(
    Guid OperationId,
    int CashRegisterId,
    decimal OpeningAmount);

public sealed record AddCashMovementCommand(
    Guid OperationId,
    string RowVersion,
    CashMovementType Type,
    decimal Amount,
    string Reason);

public sealed record CloseCashSessionCommand(string RowVersion, decimal DeclaredClosingAmount);

public sealed record CashOperationResult(CashSessionDetails Session, bool WasAlreadyProcessed);

public interface ICashService
{
    Task<IReadOnlyCollection<CashRegisterSummary>> ListRegistersAsync(
        Guid userId,
        int? storeId,
        CancellationToken cancellationToken);

    Task<CashSessionDetails?> GetCurrentAsync(
        Guid userId,
        int cashRegisterId,
        CancellationToken cancellationToken);

    Task<PagedResult<CashSessionSummary>> ListSessionsAsync(
        Guid userId,
        CashSessionListQuery query,
        CancellationToken cancellationToken);

    Task<CashSessionDetails> GetSessionAsync(
        Guid userId,
        long cashSessionId,
        CancellationToken cancellationToken);

    Task<CashOperationResult> OpenAsync(
        Guid userId,
        string correlationId,
        OpenCashSessionCommand command,
        CancellationToken cancellationToken);

    Task<CashOperationResult> AddMovementAsync(
        Guid userId,
        string correlationId,
        long cashSessionId,
        AddCashMovementCommand command,
        CancellationToken cancellationToken);

    Task<CashSessionDetails> CloseAsync(
        Guid userId,
        long cashSessionId,
        CloseCashSessionCommand command,
        CancellationToken cancellationToken);
}
