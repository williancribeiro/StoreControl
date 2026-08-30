using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Application.Purchasing;
using StoreControl.Domain.Inventory;
using StoreControl.Domain.Purchasing;
using StoreControl.Domain.Security;
using StoreControl.Domain.Stores;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Purchasing;

internal sealed class PurchaseService(
    StoreControlDbContext dbContext,
    ILogger<PurchaseService> logger) : IPurchaseService
{
    private static readonly string[] AllowedSorts = ["createdAt", "expectedDate", "supplier", "store", "total", "status"];

    public async Task<PagedResult<PurchaseOrderSummary>> ListAsync(
        Guid userId,
        PurchaseOrderListQuery query,
        CancellationToken cancellationToken)
    {
        var descending = MasterDataPersistence.ValidateQuery(
            query.Page,
            query.PageSize,
            query.SortBy,
            AllowedSorts,
            query.SortDirection);
        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc > query.ToUtc)
        {
            throw new RequestValidationException("fromUtc", "A data inicial não pode ser posterior à data final.");
        }

        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        EnsureRequestedStoreIsAccessible(query.StoreId, accessibleStoreIds);
        var orders = dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(order => accessibleStoreIds.Contains(order.StoreId));

        if (query.StoreId.HasValue)
        {
            orders = orders.Where(order => order.StoreId == query.StoreId.Value);
        }

        if (query.SupplierId.HasValue)
        {
            orders = orders.Where(order => order.SupplierId == query.SupplierId.Value);
        }

        if (query.Status.HasValue)
        {
            orders = orders.Where(order => order.Status == query.Status.Value);
        }

        if (query.FromUtc.HasValue)
        {
            orders = orders.Where(order => order.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            orders = orders.Where(order => order.CreatedAtUtc <= query.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var numericSearch = long.TryParse(search.TrimStart('#'), out var parsedId) ? parsedId : (long?)null;
            orders = orders.Where(order =>
                (numericSearch.HasValue && order.Id == numericSearch.Value) ||
                order.Supplier.Code.Contains(search) ||
                order.Supplier.Name.Contains(search) ||
                order.Store.Code.Contains(search) ||
                order.Items.Any(item =>
                    item.ProductCode.Contains(search) || item.ProductName.Contains(search)));
        }

        var totalCount = await orders.CountAsync(cancellationToken);
        orders = (query.SortBy.ToLowerInvariant(), descending) switch
        {
            ("expecteddate", false) => orders.OrderBy(order => order.ExpectedDate).ThenBy(order => order.Id),
            ("expecteddate", true) => orders.OrderByDescending(order => order.ExpectedDate).ThenByDescending(order => order.Id),
            ("supplier", false) => orders.OrderBy(order => order.Supplier.Name).ThenByDescending(order => order.Id),
            ("supplier", true) => orders.OrderByDescending(order => order.Supplier.Name).ThenByDescending(order => order.Id),
            ("store", false) => orders.OrderBy(order => order.Store.Name).ThenByDescending(order => order.Id),
            ("store", true) => orders.OrderByDescending(order => order.Store.Name).ThenByDescending(order => order.Id),
            ("total", false) => orders.OrderBy(order => order.TotalAmount).ThenByDescending(order => order.Id),
            ("total", true) => orders.OrderByDescending(order => order.TotalAmount).ThenByDescending(order => order.Id),
            ("status", false) => orders.OrderBy(order => order.Status).ThenByDescending(order => order.Id),
            ("status", true) => orders.OrderByDescending(order => order.Status).ThenByDescending(order => order.Id),
            ("createdat", false) => orders.OrderBy(order => order.CreatedAtUtc),
            _ => orders.OrderByDescending(order => order.CreatedAtUtc)
        };

        var items = await orders
            .Include(order => order.Store)
            .Include(order => order.Supplier)
            .Include(order => order.Items)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseOrderSummary>(
            items.Select(MapSummary).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<PurchaseOrderDetails> GetAsync(
        Guid userId,
        long purchaseOrderId,
        CancellationToken cancellationToken)
    {
        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        var order = await DetailsQuery(false)
            .SingleOrDefaultAsync(item => item.Id == purchaseOrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Pedido de compra não encontrado.");
        EnsureStoreIsAccessible(order.StoreId, accessibleStoreIds);
        return await MapDetailsAsync(order, cancellationToken);
    }

    public async Task<PurchaseOrderDetails> CreateAsync(
        Guid userId,
        CreatePurchaseOrderCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Items.Count is < 1 or > 100)
        {
            throw new RequestValidationException("items", "Informe entre 1 e 100 itens no pedido.");
        }

        if (command.Items.Select(item => item.ProductId).Distinct().Count() != command.Items.Count)
        {
            throw new RequestValidationException("items", "Um produto não pode aparecer mais de uma vez no pedido.");
        }

        await GetAccessibleActiveStoreAsync(userId, command.StoreId, cancellationToken);
        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == command.SupplierId, cancellationToken)
            ?? throw new RequestValidationException("supplierId", "O fornecedor informado não existe.");
        if (!supplier.IsActive)
        {
            throw new RequestValidationException("supplierId", "O fornecedor informado está inativo.");
        }

        var productIds = command.Items.Select(item => item.ProductId).ToArray();
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);
        if (products.Count != productIds.Length)
        {
            throw new RequestValidationException("items", "Um ou mais produtos informados não existem.");
        }

        if (products.Values.Any(product => !product.IsActive))
        {
            throw new RequestValidationException("items", "Produtos inativos não podem ser incluídos em novos pedidos.");
        }

        PurchaseOrder order;
        try
        {
            order = new PurchaseOrder(
                command.StoreId,
                command.SupplierId,
                userId,
                command.ExpectedDate,
                command.Notes);
            foreach (var input in command.Items)
            {
                var product = products[input.ProductId];
                order.AddItem(
                    product.Id,
                    product.Code,
                    product.Name,
                    input.Quantity,
                    input.UnitCost);
            }
        }
        catch (ArgumentException exception)
        {
            throw Validation(exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new RequestValidationException("items", exception.Message);
        }

        dbContext.PurchaseOrders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Purchase order {PurchaseOrderId} created for store {StoreId} and supplier {SupplierId}",
            order.Id,
            order.StoreId,
            order.SupplierId);
        return await GetAsync(userId, order.Id, cancellationToken);
    }

    public Task<PurchaseOrderDetails> SubmitAsync(
        Guid userId,
        long purchaseOrderId,
        PurchaseTransitionCommand command,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            userId,
            purchaseOrderId,
            command.RowVersion,
            order => order.Submit(userId),
            "Purchase order {PurchaseOrderId} submitted",
            cancellationToken);

    public async Task<ReceivePurchaseResult> ReceiveAsync(
        Guid userId,
        string correlationId,
        long purchaseOrderId,
        ReceivePurchaseCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ReceiptId == Guid.Empty)
        {
            throw new RequestValidationException("Idempotency-Key", "Informe um UUID válido no cabeçalho Idempotency-Key.");
        }

        if (command.Items.Count is < 1 or > 100)
        {
            throw new RequestValidationException("items", "Informe entre 1 e 100 itens no recebimento.");
        }

        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var wasReplay = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            try
            {
                var order = await LockedDetailsQuery(purchaseOrderId)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new ResourceNotFoundException("Pedido de compra não encontrado.");
                EnsureStoreIsAccessible(order.StoreId, accessibleStoreIds);

                if (order.Receipts.Any(receipt => receipt.Id == command.ReceiptId))
                {
                    await transaction.CommitAsync(cancellationToken);
                    return true;
                }

                if (!order.Store.IsActive)
                {
                    throw new RequestValidationException("storeId", "A loja do pedido está inativa.");
                }

                ApplyRowVersion(order, command.RowVersion);
                PurchaseReceipt receipt;
                try
                {
                    receipt = order.Receive(
                        command.ReceiptId,
                        command.Items
                            .Select(item => (item.PurchaseOrderItemId, item.Quantity))
                            .ToArray(),
                        userId,
                        command.Notes);
                }
                catch (ArgumentException exception)
                {
                    throw Validation(exception);
                }
                catch (InvalidOperationException exception)
                {
                    throw new ResourceConflictException(exception.Message);
                }

                var receiptItems = receipt.Items.OrderBy(item => item.ProductId).ToArray();
                var productIds = receiptItems.Select(item => item.ProductId).ToArray();
                var balances = await dbContext.StockBalances
                    .Where(balance => balance.StoreId == order.StoreId && productIds.Contains(balance.ProductId))
                    .OrderBy(balance => balance.ProductId)
                    .ToDictionaryAsync(balance => balance.ProductId, cancellationToken);

                foreach (var receiptItem in receiptItems)
                {
                    if (!balances.TryGetValue(receiptItem.ProductId, out var balance))
                    {
                        balance = new StockBalance(order.StoreId, receiptItem.ProductId);
                        dbContext.StockBalances.Add(balance);
                        balances.Add(receiptItem.ProductId, balance);
                    }

                    var movement = balance.ApplyMovement(
                        StockMovementType.PurchaseReceipt,
                        receiptItem.Quantity,
                        $"Recebimento do pedido {FormatNumber(order.Id)}.",
                        userId,
                        correlationId,
                        "PurchaseReceipt",
                        receipt.Id.ToString("D"));
                    dbContext.StockMovements.Add(movement);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation(
                    "Purchase receipt {ReceiptId} completed for order {PurchaseOrderId} with {ItemCount} items",
                    receipt.Id,
                    order.Id,
                    receiptItems.Length);
                return false;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new ResourceConflictException(
                    "O pedido ou o estoque foi alterado por outra operação. Recarregue os dados antes de tentar novamente.");
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new ResourceConflictException(
                    "O recebimento não foi concluído por um conflito de persistência. Nenhuma alteração foi aplicada.");
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });

        return new ReceivePurchaseResult(
            await GetAsync(userId, purchaseOrderId, cancellationToken),
            command.ReceiptId,
            wasReplay);
    }

    public Task<PurchaseOrderDetails> FinalizeAsync(
        Guid userId,
        long purchaseOrderId,
        PurchaseTransitionCommand command,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            userId,
            purchaseOrderId,
            command.RowVersion,
            order => order.Finalize(userId),
            "Purchase order {PurchaseOrderId} finalized",
            cancellationToken);

    public Task<PurchaseOrderDetails> CancelAsync(
        Guid userId,
        long purchaseOrderId,
        PurchaseTransitionCommand command,
        CancellationToken cancellationToken) =>
        TransitionAsync(
            userId,
            purchaseOrderId,
            command.RowVersion,
            order => order.Cancel(userId, command.Reason ?? string.Empty),
            "Purchase order {PurchaseOrderId} cancelled",
            cancellationToken);

    private async Task<PurchaseOrderDetails> TransitionAsync(
        Guid userId,
        long purchaseOrderId,
        string rowVersion,
        Action<PurchaseOrder> transition,
        string logMessage,
        CancellationToken cancellationToken)
    {
        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        var order = await DetailsQuery(true)
            .SingleOrDefaultAsync(item => item.Id == purchaseOrderId, cancellationToken)
            ?? throw new ResourceNotFoundException("Pedido de compra não encontrado.");
        EnsureStoreIsAccessible(order.StoreId, accessibleStoreIds);
        ApplyRowVersion(order, rowVersion);
        try
        {
            transition(order);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException exception)
        {
            throw Validation(exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new ResourceConflictException(exception.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ResourceConflictException(
                "O pedido foi alterado por outra operação. Recarregue os dados antes de tentar novamente.");
        }

        logger.LogInformation(logMessage, order.Id);
        return await GetAsync(userId, purchaseOrderId, cancellationToken);
    }

    private IQueryable<PurchaseOrder> DetailsQuery(bool tracking)
    {
        var query = dbContext.PurchaseOrders
            .Include(order => order.Store)
            .Include(order => order.Supplier)
            .Include(order => order.Items)
            .Include(order => order.Receipts)
                .ThenInclude(receipt => receipt.Items)
                    .ThenInclude(item => item.PurchaseOrderItem)
            .Include(order => order.StatusHistory)
            .AsSplitQuery();
        return tracking ? query : query.AsNoTracking();
    }

    private IQueryable<PurchaseOrder> LockedDetailsQuery(long purchaseOrderId) =>
        dbContext.PurchaseOrders
            .FromSqlInterpolated($"""
                SELECT *
                FROM [purchasing].[PurchaseOrders] WITH (UPDLOCK, ROWLOCK)
                WHERE [Id] = {purchaseOrderId}
                """)
            .Include(order => order.Store)
            .Include(order => order.Supplier)
            .Include(order => order.Items)
            .Include(order => order.Receipts)
                .ThenInclude(receipt => receipt.Items)
                    .ThenInclude(item => item.PurchaseOrderItem)
            .Include(order => order.StatusHistory)
            .AsSplitQuery();

    private async Task<PurchaseOrderDetails> MapDetailsAsync(
        PurchaseOrder order,
        CancellationToken cancellationToken)
    {
        var userIds = order.StatusHistory.Select(history => history.ChangedByUserId)
            .Concat(order.Receipts.Select(receipt => receipt.ReceivedByUserId))
            .Append(order.CreatedByUserId)
            .Distinct()
            .ToArray();
        var userNames = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        return new PurchaseOrderDetails(
            order.Id,
            FormatNumber(order.Id),
            order.StoreId,
            order.Store.Code,
            order.Store.Name,
            order.SupplierId,
            order.Supplier.Code,
            order.Supplier.Name,
            order.CreatedByUserId,
            userNames[order.CreatedByUserId],
            order.Status.ToString(),
            order.CreatedAtUtc,
            order.UpdatedAtUtc,
            order.ExpectedDate,
            order.Notes,
            order.TotalAmount,
            Convert.ToBase64String(order.RowVersion),
            order.Items
                .OrderBy(item => item.Id)
                .Select(MapItem)
                .ToArray(),
            order.Receipts
                .OrderByDescending(receipt => receipt.ReceivedAtUtc)
                .Select(receipt => new PurchaseReceiptSummary(
                    receipt.Id,
                    receipt.ReceivedAtUtc,
                    receipt.ReceivedByUserId,
                    userNames[receipt.ReceivedByUserId],
                    receipt.Notes,
                    receipt.Items
                        .OrderBy(item => item.PurchaseOrderItemId)
                        .Select(item => new PurchaseReceiptItemSummary(
                            item.PurchaseOrderItemId,
                            item.ProductId,
                            item.PurchaseOrderItem.ProductCode,
                            item.PurchaseOrderItem.ProductName,
                            item.Quantity))
                        .ToArray()))
                .ToArray(),
            order.StatusHistory
                .OrderByDescending(history => history.ChangedAtUtc)
                .ThenByDescending(history => history.Id)
                .Select(history => new PurchaseStatusHistorySummary(
                    history.Id,
                    history.Status.ToString(),
                    history.ChangedAtUtc,
                    history.ChangedByUserId,
                    userNames[history.ChangedByUserId],
                    history.Description))
                .ToArray());
    }

    private static PurchaseOrderSummary MapSummary(PurchaseOrder order) =>
        new(
            order.Id,
            FormatNumber(order.Id),
            order.StoreId,
            order.Store.Code,
            order.Store.Name,
            order.SupplierId,
            order.Supplier.Code,
            order.Supplier.Name,
            order.Status.ToString(),
            order.Items.Count,
            order.TotalAmount,
            order.CreatedAtUtc,
            order.ExpectedDate,
            Convert.ToBase64String(order.RowVersion));

    private static PurchaseOrderItemSummary MapItem(PurchaseOrderItem item) =>
        new(
            item.Id,
            item.ProductId,
            item.ProductCode,
            item.ProductName,
            item.OrderedQuantity,
            item.ReceivedQuantity,
            item.RemainingQuantity,
            item.UnitCost,
            item.LineTotal);

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

    private async Task<Store> GetAccessibleActiveStoreAsync(
        Guid userId,
        int storeId,
        CancellationToken cancellationToken)
    {
        var store = await dbContext.Stores
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == storeId, cancellationToken)
            ?? throw new ResourceNotFoundException("Loja não encontrada.");
        var accessibleStoreIds = await GetAccessibleStoreIdsAsync(userId, cancellationToken);
        EnsureStoreIsAccessible(storeId, accessibleStoreIds);
        if (!store.IsActive)
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

    private void ApplyRowVersion(PurchaseOrder order, string rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            throw new RequestValidationException("rowVersion", "Informe a versão atual do pedido.");
        }

        dbContext.Entry(order).Property(item => item.RowVersion).OriginalValue =
            MasterDataPersistence.ParseRowVersion(rowVersion);
    }

    private static void EnsureRequestedStoreIsAccessible(
        int? storeId,
        IReadOnlySet<int> accessibleStoreIds)
    {
        if (storeId.HasValue)
        {
            EnsureStoreIsAccessible(storeId.Value, accessibleStoreIds);
        }
    }

    private static void EnsureStoreIsAccessible(int storeId, IReadOnlySet<int> accessibleStoreIds)
    {
        if (!accessibleStoreIds.Contains(storeId))
        {
            throw new OperationForbiddenException("O usuário não possui acesso à loja informada.");
        }
    }

    private static string FormatNumber(long id) => $"PC-{id:000000}";

    private static RequestValidationException Validation(ArgumentException exception) =>
        new(exception.ParamName ?? "purchase", exception.Message);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
