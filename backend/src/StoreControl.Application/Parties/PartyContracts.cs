using StoreControl.Application.Common;

namespace StoreControl.Application.Parties;

public sealed record PartyQuery(
    int Page,
    int PageSize,
    string? Search,
    bool? IsActive,
    string SortBy,
    string SortDirection);

public sealed record CustomerSummary(
    int Id,
    string Code,
    string Name,
    string? Document,
    string? Email,
    string? Phone,
    bool IsActive,
    string RowVersion);

public sealed record CreateCustomerCommand(
    string Code,
    string Name,
    string? Document,
    string? Email,
    string? Phone);

public sealed record UpdateCustomerCommand(
    string Code,
    string Name,
    string? Document,
    string? Email,
    string? Phone,
    bool IsActive,
    string RowVersion);

public interface ICustomerService
{
    Task<PagedResult<CustomerSummary>> ListAsync(PartyQuery query, CancellationToken cancellationToken);

    Task<CustomerSummary> CreateAsync(CreateCustomerCommand command, CancellationToken cancellationToken);

    Task<CustomerSummary> UpdateAsync(
        int customerId,
        UpdateCustomerCommand command,
        CancellationToken cancellationToken);
}

public sealed record SupplierSummary(
    int Id,
    string Code,
    string Name,
    string? Document,
    string? Email,
    string? Phone,
    bool IsActive,
    string RowVersion);

public sealed record CreateSupplierCommand(
    string Code,
    string Name,
    string? Document,
    string? Email,
    string? Phone);

public sealed record UpdateSupplierCommand(
    string Code,
    string Name,
    string? Document,
    string? Email,
    string? Phone,
    bool IsActive,
    string RowVersion);

public interface ISupplierService
{
    Task<PagedResult<SupplierSummary>> ListAsync(PartyQuery query, CancellationToken cancellationToken);

    Task<SupplierSummary> CreateAsync(CreateSupplierCommand command, CancellationToken cancellationToken);

    Task<SupplierSummary> UpdateAsync(
        int supplierId,
        UpdateSupplierCommand command,
        CancellationToken cancellationToken);
}
