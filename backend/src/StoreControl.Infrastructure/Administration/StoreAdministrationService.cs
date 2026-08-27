using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StoreControl.Application.Administration;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Domain.Security;
using StoreControl.Domain.Stores;
using StoreControl.Infrastructure.Identity;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Administration;

internal sealed class StoreAdministrationService(StoreControlDbContext dbContext)
    : IStoreAdministrationService
{
    public async Task<IReadOnlyCollection<StoreSummary>> ListAccessibleAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var isAdministrator = await (
            from userRole in dbContext.Set<IdentityUserRole<Guid>>()
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == userId && role.Name == RoleNames.Administrator
            select userRole).AnyAsync(cancellationToken);

        var query = dbContext.Stores.AsNoTracking();
        if (!isAdministrator)
        {
            query = query.Where(store => store.IsActive && dbContext.UserStoreRoles.Any(
                assignment => assignment.UserId == userId && assignment.StoreId == store.Id));
        }

        return await query
            .OrderBy(store => store.Name)
            .Select(store => new StoreSummary(
                store.Id,
                store.Code,
                store.Name,
                store.IsActive,
                Convert.ToBase64String(store.RowVersion)))
            .ToListAsync(cancellationToken);
    }

    public async Task<StoreSummary> CreateAsync(
        CreateStoreCommand command,
        CancellationToken cancellationToken)
    {
        Store store;
        try
        {
            store = new Store(command.Code, command.Name);
        }
        catch (ArgumentException exception)
        {
            throw new RequestValidationException(exception.ParamName ?? "store", exception.Message);
        }

        if (await dbContext.Stores.AnyAsync(candidate => candidate.Code == store.Code, cancellationToken))
        {
            throw new ResourceConflictException($"Já existe uma loja com o código {store.Code}.");
        }

        dbContext.Stores.Add(store);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ResourceConflictException($"Já existe uma loja com o código {store.Code}.");
        }

        return Map(store);
    }

    public async Task<StoreSummary> UpdateAsync(
        int storeId,
        UpdateStoreCommand command,
        CancellationToken cancellationToken)
    {
        var store = await dbContext.Stores.SingleOrDefaultAsync(candidate => candidate.Id == storeId, cancellationToken)
            ?? throw new ResourceNotFoundException("Loja não encontrada.");

        byte[] expectedRowVersion;
        try
        {
            expectedRowVersion = Convert.FromBase64String(command.RowVersion);
        }
        catch (FormatException)
        {
            throw new RequestValidationException("rowVersion", "A versão de concorrência é inválida.");
        }

        try
        {
            store.Update(command.Code, command.Name, command.IsActive);
        }
        catch (ArgumentException exception)
        {
            throw new RequestValidationException(exception.ParamName ?? "store", exception.Message);
        }

        dbContext.Entry(store).Property(candidate => candidate.RowVersion).OriginalValue = expectedRowVersion;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ResourceConflictException(
                "A loja foi alterada por outro usuário. Recarregue os dados antes de tentar novamente.");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ResourceConflictException($"Já existe uma loja com o código {store.Code}.");
        }

        return Map(store);
    }

    private static StoreSummary Map(Store store) =>
        new(store.Id, store.Code, store.Name, store.IsActive, Convert.ToBase64String(store.RowVersion));

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
