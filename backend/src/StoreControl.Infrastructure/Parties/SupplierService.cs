using Microsoft.EntityFrameworkCore;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Application.Parties;
using StoreControl.Domain.Parties;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Parties;

internal sealed class SupplierService(StoreControlDbContext dbContext) : ISupplierService
{
    private static readonly string[] AllowedSorts = ["code", "name", "createdAt"];

    public async Task<PagedResult<SupplierSummary>> ListAsync(
        PartyQuery query,
        CancellationToken cancellationToken)
    {
        var descending = MasterDataPersistence.ValidateQuery(
            query.Page,
            query.PageSize,
            query.SortBy,
            AllowedSorts,
            query.SortDirection);
        var suppliers = dbContext.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            suppliers = suppliers.Where(supplier =>
                supplier.Code.Contains(search) ||
                supplier.Name.Contains(search) ||
                (supplier.Document != null && supplier.Document.Contains(search)) ||
                (supplier.Email != null && supplier.Email.Contains(search)) ||
                (supplier.Phone != null && supplier.Phone.Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            suppliers = suppliers.Where(supplier => supplier.IsActive == query.IsActive.Value);
        }

        var totalCount = await suppliers.CountAsync(cancellationToken);
        suppliers = (query.SortBy.ToLowerInvariant(), descending) switch
        {
            ("code", false) => suppliers.OrderBy(supplier => supplier.Code),
            ("code", true) => suppliers.OrderByDescending(supplier => supplier.Code),
            ("createdat", false) => suppliers.OrderBy(supplier => supplier.CreatedAtUtc),
            ("createdat", true) => suppliers.OrderByDescending(supplier => supplier.CreatedAtUtc),
            ("name", true) => suppliers.OrderByDescending(supplier => supplier.Name),
            _ => suppliers.OrderBy(supplier => supplier.Name)
        };
        var items = await suppliers
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SupplierSummary>(
            items.Select(Map).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<SupplierSummary> CreateAsync(
        CreateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        var supplier = CreateEntity(command);
        await EnsureUniqueAsync(supplier, null, cancellationToken);
        dbContext.Suppliers.Add(supplier);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (MasterDataPersistence.IsUniqueConstraintViolation(exception))
        {
            throw Duplicate(supplier, exception);
        }

        return Map(supplier);
    }

    public async Task<SupplierSummary> UpdateAsync(
        int supplierId,
        UpdateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(item => item.Id == supplierId, cancellationToken)
            ?? throw new ResourceNotFoundException("Fornecedor não encontrado.");
        var expectedRowVersion = MasterDataPersistence.ParseRowVersion(command.RowVersion);
        try
        {
            supplier.Update(
                command.Code,
                command.Name,
                command.Document,
                command.Email,
                command.Phone,
                command.IsActive);
        }
        catch (ArgumentException exception)
        {
            throw Validation(exception);
        }

        await EnsureUniqueAsync(supplier, supplierId, cancellationToken);
        dbContext.Entry(supplier).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ResourceConflictException(
                "O fornecedor foi alterado por outro usuário. Recarregue os dados antes de tentar novamente.");
        }
        catch (DbUpdateException exception) when (MasterDataPersistence.IsUniqueConstraintViolation(exception))
        {
            throw Duplicate(supplier, exception);
        }

        return Map(supplier);
    }

    private async Task EnsureUniqueAsync(Supplier supplier, int? currentId, CancellationToken cancellationToken)
    {
        if (await dbContext.Suppliers.AnyAsync(
                item => item.Code == supplier.Code && (!currentId.HasValue || item.Id != currentId.Value),
                cancellationToken))
        {
            throw new ResourceConflictException($"Já existe um fornecedor com o código {supplier.Code}.");
        }

        if (supplier.Document is not null && await dbContext.Suppliers.AnyAsync(
                item => item.Document == supplier.Document && (!currentId.HasValue || item.Id != currentId.Value),
                cancellationToken))
        {
            throw new ResourceConflictException($"Já existe um fornecedor com o documento {supplier.Document}.");
        }
    }

    private static Supplier CreateEntity(CreateSupplierCommand command)
    {
        try
        {
            return new Supplier(command.Code, command.Name, command.Document, command.Email, command.Phone);
        }
        catch (ArgumentException exception)
        {
            throw Validation(exception);
        }
    }

    private static SupplierSummary Map(Supplier supplier) =>
        new(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.Document,
            supplier.Email,
            supplier.Phone,
            supplier.IsActive,
            Convert.ToBase64String(supplier.RowVersion));

    private static RequestValidationException Validation(ArgumentException exception) =>
        new(exception.ParamName ?? "supplier", exception.Message);

    private static ResourceConflictException Duplicate(Supplier supplier, DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("Document", StringComparison.OrdinalIgnoreCase) == true
            ? new ResourceConflictException($"Já existe um fornecedor com o documento {supplier.Document}.")
            : new ResourceConflictException($"Já existe um fornecedor com o código {supplier.Code}.");
}
