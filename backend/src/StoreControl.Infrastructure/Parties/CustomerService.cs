using Microsoft.EntityFrameworkCore;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Application.Parties;
using StoreControl.Domain.Parties;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Parties;

internal sealed class CustomerService(StoreControlDbContext dbContext) : ICustomerService
{
    private static readonly string[] AllowedSorts = ["code", "name", "createdAt"];

    public async Task<PagedResult<CustomerSummary>> ListAsync(
        PartyQuery query,
        CancellationToken cancellationToken)
    {
        var descending = MasterDataPersistence.ValidateQuery(
            query.Page,
            query.PageSize,
            query.SortBy,
            AllowedSorts,
            query.SortDirection);
        var customers = dbContext.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            customers = customers.Where(customer =>
                customer.Code.Contains(search) ||
                customer.Name.Contains(search) ||
                (customer.Document != null && customer.Document.Contains(search)) ||
                (customer.Email != null && customer.Email.Contains(search)) ||
                (customer.Phone != null && customer.Phone.Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            customers = customers.Where(customer => customer.IsActive == query.IsActive.Value);
        }

        var totalCount = await customers.CountAsync(cancellationToken);
        customers = (query.SortBy.ToLowerInvariant(), descending) switch
        {
            ("code", false) => customers.OrderBy(customer => customer.Code),
            ("code", true) => customers.OrderByDescending(customer => customer.Code),
            ("createdat", false) => customers.OrderBy(customer => customer.CreatedAtUtc),
            ("createdat", true) => customers.OrderByDescending(customer => customer.CreatedAtUtc),
            ("name", true) => customers.OrderByDescending(customer => customer.Name),
            _ => customers.OrderBy(customer => customer.Name)
        };
        var items = await customers
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerSummary>(
            items.Select(Map).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<CustomerSummary> CreateAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var customer = CreateEntity(command);
        await EnsureUniqueAsync(customer, null, cancellationToken);
        dbContext.Customers.Add(customer);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (MasterDataPersistence.IsUniqueConstraintViolation(exception))
        {
            throw Duplicate(customer, exception);
        }

        return Map(customer);
    }

    public async Task<CustomerSummary> UpdateAsync(
        int customerId,
        UpdateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.SingleOrDefaultAsync(item => item.Id == customerId, cancellationToken)
            ?? throw new ResourceNotFoundException("Cliente não encontrado.");
        var expectedRowVersion = MasterDataPersistence.ParseRowVersion(command.RowVersion);
        try
        {
            customer.Update(
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

        await EnsureUniqueAsync(customer, customerId, cancellationToken);
        dbContext.Entry(customer).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ResourceConflictException(
                "O cliente foi alterado por outro usuário. Recarregue os dados antes de tentar novamente.");
        }
        catch (DbUpdateException exception) when (MasterDataPersistence.IsUniqueConstraintViolation(exception))
        {
            throw Duplicate(customer, exception);
        }

        return Map(customer);
    }

    private async Task EnsureUniqueAsync(Customer customer, int? currentId, CancellationToken cancellationToken)
    {
        if (await dbContext.Customers.AnyAsync(
                item => item.Code == customer.Code && (!currentId.HasValue || item.Id != currentId.Value),
                cancellationToken))
        {
            throw new ResourceConflictException($"Já existe um cliente com o código {customer.Code}.");
        }

        if (customer.Document is not null && await dbContext.Customers.AnyAsync(
                item => item.Document == customer.Document && (!currentId.HasValue || item.Id != currentId.Value),
                cancellationToken))
        {
            throw new ResourceConflictException($"Já existe um cliente com o documento {customer.Document}.");
        }
    }

    private static Customer CreateEntity(CreateCustomerCommand command)
    {
        try
        {
            return new Customer(command.Code, command.Name, command.Document, command.Email, command.Phone);
        }
        catch (ArgumentException exception)
        {
            throw Validation(exception);
        }
    }

    private static CustomerSummary Map(Customer customer) =>
        new(
            customer.Id,
            customer.Code,
            customer.Name,
            customer.Document,
            customer.Email,
            customer.Phone,
            customer.IsActive,
            Convert.ToBase64String(customer.RowVersion));

    private static RequestValidationException Validation(ArgumentException exception) =>
        new(exception.ParamName ?? "customer", exception.Message);

    private static ResourceConflictException Duplicate(Customer customer, DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("Document", StringComparison.OrdinalIgnoreCase) == true
            ? new ResourceConflictException($"Já existe um cliente com o documento {customer.Document}.")
            : new ResourceConflictException($"Já existe um cliente com o código {customer.Code}.");
}
