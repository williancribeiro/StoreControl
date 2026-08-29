using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Application.Inventory;
using StoreControl.Domain.Catalog;
using StoreControl.Domain.Inventory;
using StoreControl.Domain.Security;
using StoreControl.Domain.Stores;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Inventory;

internal sealed class InventoryService(
    StoreControlDbContext dbContext,
    ILogger<InventoryService> logger) : IInventoryService
{
    private static readonly string[] PositionSorts = ["product", "store", "quantity", "minimum", "updatedAt"];
    private static readonly string[] MovementSorts = ["occurredAt", "product", "store", "quantity"];

    public async Task<PagedResult<StockPositionSummary>> ListPositionsAsync(
        Guid userId,
        StockPositionQuery query,
        CancellationToken cancellationToken)
    {
        var descending = MasterDataPersistence.ValidateQuery(
            query.Page,
            query.PageSize,
            query.SortBy,
            PositionSorts,
            query.SortDirection);
        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        EnsureRequestedStoreIsAccessible(query.StoreId, accessibleStoreIds);

        var positions =
            from store in dbContext.Stores.AsNoTracking()
            from product in dbContext.Products.AsNoTracking()
            join balance in dbContext.StockBalances.AsNoTracking()
                on new { StoreId = store.Id, ProductId = product.Id }
                equals new { balance.StoreId, balance.ProductId }
                into balanceGroup
            from balance in balanceGroup.DefaultIfEmpty()
            where accessibleStoreIds.Contains(store.Id)
            select new { Store = store, Product = product, Balance = balance };

        if (query.StoreId.HasValue)
        {
            positions = positions.Where(position => position.Store.Id == query.StoreId.Value);
        }

        if (query.ProductId.HasValue)
        {
            positions = positions.Where(position => position.Product.Id == query.ProductId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            positions = positions.Where(position =>
                position.Product.Code.Contains(search) ||
                (position.Product.Barcode != null && position.Product.Barcode.Contains(search)) ||
                position.Product.Name.Contains(search) ||
                position.Store.Code.Contains(search) ||
                position.Store.Name.Contains(search));
        }

        if (query.BelowMinimum.HasValue)
        {
            positions = query.BelowMinimum.Value
                ? positions.Where(position =>
                    position.Balance != null &&
                    position.Balance.MinimumQuantity > 0 &&
                    position.Balance.QuantityOnHand <= position.Balance.MinimumQuantity)
                : positions.Where(position =>
                    position.Balance == null ||
                    position.Balance.MinimumQuantity == 0 ||
                    position.Balance.QuantityOnHand > position.Balance.MinimumQuantity);
        }

        var totalCount = await positions.CountAsync(cancellationToken);
        positions = (query.SortBy.ToLowerInvariant(), descending) switch
        {
            ("store", false) => positions.OrderBy(position => position.Store.Name).ThenBy(position => position.Product.Name),
            ("store", true) => positions.OrderByDescending(position => position.Store.Name).ThenBy(position => position.Product.Name),
            ("quantity", false) => positions.OrderBy(position => position.Balance == null ? 0 : position.Balance.QuantityOnHand).ThenBy(position => position.Product.Name),
            ("quantity", true) => positions.OrderByDescending(position => position.Balance == null ? 0 : position.Balance.QuantityOnHand).ThenBy(position => position.Product.Name),
            ("minimum", false) => positions.OrderBy(position => position.Balance == null ? 0 : position.Balance.MinimumQuantity).ThenBy(position => position.Product.Name),
            ("minimum", true) => positions.OrderByDescending(position => position.Balance == null ? 0 : position.Balance.MinimumQuantity).ThenBy(position => position.Product.Name),
            ("updatedat", false) => positions.OrderBy(position => position.Balance == null ? DateTime.MinValue : position.Balance.UpdatedAtUtc),
            ("updatedat", true) => positions.OrderByDescending(position => position.Balance == null ? DateTime.MinValue : position.Balance.UpdatedAtUtc),
            ("product", true) => positions.OrderByDescending(position => position.Product.Name).ThenBy(position => position.Store.Name),
            _ => positions.OrderBy(position => position.Product.Name).ThenBy(position => position.Store.Name)
        };

        var items = await positions
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StockPositionSummary>(
            items.Select(position => position.Balance is null
                ? MapEmptyPosition(position.Store, position.Product)
                : MapPosition(position.Balance, position.Store, position.Product)).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<StockPositionSummary> GetPositionAsync(
        Guid userId,
        int storeId,
        int productId,
        CancellationToken cancellationToken)
    {
        var store = await GetAccessibleStoreAsync(userId, storeId, false, cancellationToken);
        var product = await GetProductAsync(productId, cancellationToken);
        var balance = await dbContext.StockBalances
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.StoreId == storeId && item.ProductId == productId,
                cancellationToken);

        return balance is null
            ? MapEmptyPosition(store, product)
            : MapPosition(balance, store, product);
    }

    public async Task<PagedResult<StockMovementSummary>> ListMovementsAsync(
        Guid userId,
        StockMovementQuery query,
        CancellationToken cancellationToken)
    {
        var descending = MasterDataPersistence.ValidateQuery(
            query.Page,
            query.PageSize,
            query.SortBy,
            MovementSorts,
            query.SortDirection);
        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc > query.ToUtc)
        {
            throw new RequestValidationException("fromUtc", "A data inicial não pode ser posterior à data final.");
        }

        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        EnsureRequestedStoreIsAccessible(query.StoreId, accessibleStoreIds);
        var movements = dbContext.StockMovements
            .AsNoTracking()
            .Where(movement => accessibleStoreIds.Contains(movement.StoreId));

        if (query.StoreId.HasValue)
        {
            movements = movements.Where(movement => movement.StoreId == query.StoreId.Value);
        }

        if (query.ProductId.HasValue)
        {
            movements = movements.Where(movement => movement.ProductId == query.ProductId.Value);
        }

        if (query.Type.HasValue)
        {
            movements = movements.Where(movement => movement.Type == query.Type.Value);
        }

        if (query.FromUtc.HasValue)
        {
            movements = movements.Where(movement => movement.OccurredAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            movements = movements.Where(movement => movement.OccurredAtUtc <= query.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            movements = movements.Where(movement =>
                movement.Product.Code.Contains(search) ||
                movement.Product.Name.Contains(search) ||
                movement.Store.Code.Contains(search) ||
                movement.Reason.Contains(search) ||
                (movement.ReferenceId != null && movement.ReferenceId.Contains(search)));
        }

        var totalCount = await movements.CountAsync(cancellationToken);
        movements = (query.SortBy.ToLowerInvariant(), descending) switch
        {
            ("product", false) => movements.OrderBy(movement => movement.Product.Name).ThenByDescending(movement => movement.OccurredAtUtc),
            ("product", true) => movements.OrderByDescending(movement => movement.Product.Name).ThenByDescending(movement => movement.OccurredAtUtc),
            ("store", false) => movements.OrderBy(movement => movement.Store.Name).ThenByDescending(movement => movement.OccurredAtUtc),
            ("store", true) => movements.OrderByDescending(movement => movement.Store.Name).ThenByDescending(movement => movement.OccurredAtUtc),
            ("quantity", false) => movements.OrderBy(movement => movement.QuantityChange).ThenByDescending(movement => movement.OccurredAtUtc),
            ("quantity", true) => movements.OrderByDescending(movement => movement.QuantityChange).ThenByDescending(movement => movement.OccurredAtUtc),
            ("occurredat", false) => movements.OrderBy(movement => movement.OccurredAtUtc),
            _ => movements.OrderByDescending(movement => movement.OccurredAtUtc)
        };

        var items = await movements
            .Include(movement => movement.Store)
            .Include(movement => movement.Product)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var userNames = await LoadUserNamesAsync(items.Select(item => item.CreatedByUserId), cancellationToken);

        return new PagedResult<StockMovementSummary>(
            items.Select(item => MapMovement(item, userNames[item.CreatedByUserId])).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<StockMovementSummary> GetMovementAsync(
        Guid userId,
        long movementId,
        CancellationToken cancellationToken)
    {
        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        var movement = await dbContext.StockMovements
            .AsNoTracking()
            .Include(item => item.Store)
            .Include(item => item.Product)
            .SingleOrDefaultAsync(
                item => item.Id == movementId && accessibleStoreIds.Contains(item.StoreId),
                cancellationToken)
            ?? throw new ResourceNotFoundException("Movimentação de estoque não encontrada.");
        var actorName = await dbContext.Users
            .Where(user => user.Id == movement.CreatedByUserId)
            .Select(user => user.FullName)
            .SingleAsync(cancellationToken);

        return MapMovement(movement, actorName);
    }

    public async Task<StockAdjustmentResult> AdjustAsync(
        Guid userId,
        string correlationId,
        CreateStockAdjustmentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Type is not (StockMovementType.PositiveAdjustment or StockMovementType.NegativeAdjustment))
        {
            throw new RequestValidationException("direction", "Apenas ajustes positivos ou negativos são aceitos.");
        }

        var store = await GetAccessibleStoreAsync(userId, command.StoreId, true, cancellationToken);
        var product = await GetProductAsync(command.ProductId, cancellationToken);
        var actorName = await dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => user.FullName)
            .SingleAsync(cancellationToken);

        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            try
            {
                var balance = await dbContext.StockBalances.SingleOrDefaultAsync(
                    item => item.StoreId == command.StoreId && item.ProductId == command.ProductId,
                    cancellationToken);
                if (balance is null)
                {
                    if (!string.IsNullOrWhiteSpace(command.RowVersion))
                    {
                        throw new ResourceConflictException(
                            "A posição de estoque mudou. Recarregue os dados antes de tentar novamente.");
                    }

                    balance = new StockBalance(command.StoreId, command.ProductId);
                    dbContext.StockBalances.Add(balance);
                }
                else
                {
                    RequireAndApplyRowVersion(balance, command.RowVersion);
                }

                StockMovement movement;
                try
                {
                    movement = balance.ApplyMovement(
                        command.Type,
                        command.Quantity,
                        command.Reason,
                        userId,
                        correlationId,
                        "ManualAdjustment");
                }
                catch (InvalidOperationException exception)
                {
                    logger.LogWarning(
                        "Insufficient stock for product {ProductId} at store {StoreId}",
                        command.ProductId,
                        command.StoreId);
                    throw new ResourceConflictException(exception.Message);
                }
                catch (ArgumentException exception)
                {
                    throw Validation(exception);
                }

                dbContext.StockMovements.Add(movement);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation(
                    "Stock movement {MovementId} applied to product {ProductId} at store {StoreId} with change {QuantityChange}",
                    movement.Id,
                    product.Id,
                    store.Id,
                    movement.QuantityChange);

                return new StockAdjustmentResult(
                    MapPosition(balance, store, product),
                    MapMovement(movement, store, product, actorName));
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new ResourceConflictException(
                    "A posição de estoque foi alterada por outra operação. Recarregue os dados antes de tentar novamente.");
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new ResourceConflictException(
                    "A posição de estoque foi criada por outra operação. Recarregue os dados antes de tentar novamente.");
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }

    public async Task<StockPositionSummary> UpdateMinimumAsync(
        Guid userId,
        int storeId,
        int productId,
        UpdateMinimumStockCommand command,
        CancellationToken cancellationToken)
    {
        var store = await GetAccessibleStoreAsync(userId, storeId, true, cancellationToken);
        var product = await GetProductAsync(productId, cancellationToken);
        var balance = await dbContext.StockBalances.SingleOrDefaultAsync(
            item => item.StoreId == storeId && item.ProductId == productId,
            cancellationToken);

        if (balance is null)
        {
            if (!string.IsNullOrWhiteSpace(command.RowVersion))
            {
                throw new ResourceConflictException(
                    "A posição de estoque mudou. Recarregue os dados antes de tentar novamente.");
            }

            try
            {
                balance = new StockBalance(storeId, productId, command.MinimumQuantity);
            }
            catch (ArgumentException exception)
            {
                throw Validation(exception);
            }

            dbContext.StockBalances.Add(balance);
        }
        else
        {
            RequireAndApplyRowVersion(balance, command.RowVersion);
            try
            {
                balance.UpdateMinimumQuantity(command.MinimumQuantity);
            }
            catch (ArgumentException exception)
            {
                throw Validation(exception);
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ResourceConflictException(
                "A posição de estoque foi alterada por outra operação. Recarregue os dados antes de tentar novamente.");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ResourceConflictException(
                "A posição de estoque foi criada por outra operação. Recarregue os dados antes de tentar novamente.");
        }

        return MapPosition(balance, store, product);
    }

    private async Task<HashSet<int>> GetAccessibleStoreIdsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (await IsAdministratorAsync(userId, cancellationToken))
        {
            return (await dbContext.Stores
                .AsNoTracking()
                .Select(store => store.Id)
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        return (await dbContext.UserStoreRoles
            .AsNoTracking()
            .Where(assignment => assignment.UserId == userId && assignment.Store.IsActive)
            .Select(assignment => assignment.StoreId)
            .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    private async Task<Store> GetAccessibleStoreAsync(
        Guid userId,
        int storeId,
        bool requireActive,
        CancellationToken cancellationToken)
    {
        var store = await dbContext.Stores
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == storeId, cancellationToken)
            ?? throw new ResourceNotFoundException("Loja não encontrada.");
        var hasAccess = await IsAdministratorAsync(userId, cancellationToken) ||
            await dbContext.UserStoreRoles.AnyAsync(
                assignment => assignment.UserId == userId && assignment.StoreId == storeId,
                cancellationToken);
        if (!hasAccess)
        {
            throw new OperationForbiddenException("O usuário não possui acesso à loja informada.");
        }

        if (requireActive && !store.IsActive)
        {
            throw new RequestValidationException("storeId", "A loja informada está inativa.");
        }

        return store;
    }

    private Task<bool> IsAdministratorAsync(Guid userId, CancellationToken cancellationToken) =>
        (from userRole in dbContext.Set<IdentityUserRole<Guid>>()
         join role in dbContext.Roles on userRole.RoleId equals role.Id
         where userRole.UserId == userId && role.Name == RoleNames.Administrator
         select userRole).AnyAsync(cancellationToken);

    private async Task<Product> GetProductAsync(int productId, CancellationToken cancellationToken) =>
        await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(product => product.Id == productId, cancellationToken)
        ?? throw new ResourceNotFoundException("Produto não encontrado.");

    private async Task<IReadOnlyDictionary<Guid, string>> LoadUserNamesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        return await dbContext.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
    }

    private void RequireAndApplyRowVersion(StockBalance balance, string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            throw new RequestValidationException(
                "rowVersion",
                "Informe a versão da posição de estoque antes de alterá-la.");
        }

        dbContext.Entry(balance).Property(item => item.RowVersion).OriginalValue =
            MasterDataPersistence.ParseRowVersion(rowVersion);
    }

    private static void EnsureRequestedStoreIsAccessible(int? storeId, IReadOnlySet<int> accessibleStoreIds)
    {
        if (storeId.HasValue && !accessibleStoreIds.Contains(storeId.Value))
        {
            throw new OperationForbiddenException("O usuário não possui acesso à loja informada.");
        }
    }

    private static StockPositionSummary MapPosition(StockBalance balance) =>
        MapPosition(balance, balance.Store, balance.Product);

    private static StockPositionSummary MapPosition(StockBalance balance, Store store, Product product) =>
        new(
            store.Id,
            store.Code,
            store.Name,
            product.Id,
            product.Code,
            product.Barcode,
            product.Name,
            product.IsActive,
            balance.QuantityOnHand,
            balance.MinimumQuantity,
            balance.IsBelowMinimum,
            Convert.ToBase64String(balance.RowVersion));

    private static StockPositionSummary MapEmptyPosition(Store store, Product product) =>
        new(
            store.Id,
            store.Code,
            store.Name,
            product.Id,
            product.Code,
            product.Barcode,
            product.Name,
            product.IsActive,
            0,
            0,
            false,
            null);

    private static StockMovementSummary MapMovement(StockMovement movement, string actorName) =>
        MapMovement(movement, movement.Store, movement.Product, actorName);

    private static StockMovementSummary MapMovement(
        StockMovement movement,
        Store store,
        Product product,
        string actorName) =>
        new(
            movement.Id,
            movement.StoreId,
            store.Code,
            store.Name,
            movement.ProductId,
            product.Code,
            product.Name,
            movement.Type.ToString(),
            movement.QuantityChange,
            movement.QuantityBefore,
            movement.QuantityAfter,
            movement.Reason,
            movement.CreatedByUserId,
            actorName,
            movement.OccurredAtUtc,
            movement.CorrelationId,
            movement.ReferenceType,
            movement.ReferenceId);

    private static RequestValidationException Validation(ArgumentException exception) =>
        new(exception.ParamName ?? "inventory", exception.Message);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
