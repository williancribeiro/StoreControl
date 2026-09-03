using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Application.Reporting;
using StoreControl.Domain.Purchasing;
using StoreControl.Domain.Security;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Reporting;

internal sealed class ReportingService(StoreControlDbContext dbContext) : IReportingService
{
    private const int MaximumPeriodDays = 366;
    private static readonly string[] InventorySorts =
        ["product", "store", "quantity", "minimum", "category"];

    public async Task<DashboardSummary> GetDashboardAsync(
        Guid userId,
        DashboardQuery query,
        CancellationToken cancellationToken)
    {
        var referenceDate = query.ReferenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var storeIds = await GetScopedStoreIdsAsync(userId, query.StoreId, cancellationToken);
        var firstEvolutionDate = referenceDate.AddDays(-6);
        var dailyRows = await dbContext.DailySalesReportRows
            .AsNoTracking()
            .Where(row =>
                storeIds.Contains(row.StoreId) &&
                row.SaleDate >= firstEvolutionDate &&
                row.SaleDate <= referenceDate)
            .ToListAsync(cancellationToken);
        var selectedDay = dailyRows.Where(row => row.SaleDate == referenceDate).ToArray();
        var saleCount = selectedDay.Sum(row => row.SaleCount);
        var revenue = Money(selectedDay.Sum(row => row.Revenue));
        var evolution = Enumerable.Range(0, 7)
            .Select(offset => firstEvolutionDate.AddDays(offset))
            .Select(date =>
            {
                var rows = dailyRows.Where(row => row.SaleDate == date).ToArray();
                return new SalesEvolutionPoint(
                    date,
                    rows.Sum(row => row.SaleCount),
                    Money(rows.Sum(row => row.Revenue)));
            })
            .ToArray();

        var lowStockQuery = dbContext.StockPositionReportRows
            .AsNoTracking()
            .Where(row => storeIds.Contains(row.StoreId) && row.IsBelowMinimum);
        var lowStockCount = await lowStockQuery.CountAsync(cancellationToken);
        var lowStockItems = await lowStockQuery
            .OrderBy(row => row.QuantityOnHand)
            .ThenBy(row => row.ProductName)
            .Take(8)
            .Select(row => new LowStockSummary(
                row.StoreId,
                row.StoreCode,
                row.StoreName,
                row.ProductId,
                row.ProductCode,
                row.ProductName,
                row.QuantityOnHand,
                row.MinimumQuantity))
            .ToListAsync(cancellationToken);
        var topProducts = await LoadTopSellingProductsAsync(
            UtcStart(referenceDate),
            UtcStart(referenceDate.AddDays(1)),
            storeIds,
            5,
            cancellationToken);

        return new DashboardSummary(
            referenceDate,
            query.StoreId,
            saleCount,
            revenue,
            saleCount == 0 ? 0 : Money(revenue / saleCount),
            lowStockCount,
            topProducts,
            lowStockItems,
            evolution);
    }

    public async Task<SalesReport> GetSalesAsync(
        Guid userId,
        SalesReportQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePeriod(query.FromDate, query.ToDate);
        var storeIds = await GetScopedStoreIdsAsync(userId, query.StoreId, cancellationToken);
        var rows = await dbContext.DailySalesReportRows
            .AsNoTracking()
            .Where(row =>
                storeIds.Contains(row.StoreId) &&
                row.SaleDate >= query.FromDate &&
                row.SaleDate <= query.ToDate)
            .ToListAsync(cancellationToken);
        var stores = await LoadStoresAsync(storeIds, cancellationToken);
        var saleCount = rows.Sum(row => row.SaleCount);
        var grossRevenue = Money(rows.Sum(row => row.GrossRevenue));
        var discount = Money(rows.Sum(row => row.DiscountAmount));
        var revenue = Money(rows.Sum(row => row.Revenue));
        var daily = rows
            .GroupBy(row => row.SaleDate)
            .OrderBy(group => group.Key)
            .Select(group => new SalesEvolutionPoint(
                group.Key,
                group.Sum(row => row.SaleCount),
                Money(group.Sum(row => row.Revenue))))
            .ToArray();
        var byStore = rows
            .GroupBy(row => row.StoreId)
            .Select(group =>
            {
                var count = group.Sum(row => row.SaleCount);
                var store = stores[group.Key];
                var storeRevenue = Money(group.Sum(row => row.Revenue));
                return new SalesByStoreSummary(
                    group.Key,
                    store.Code,
                    store.Name,
                    count,
                    storeRevenue,
                    count == 0 ? 0 : Money(storeRevenue / count));
            })
            .OrderByDescending(row => row.Revenue)
            .ToArray();
        var topProducts = await LoadTopSellingProductsAsync(
            UtcStart(query.FromDate),
            UtcStart(query.ToDate.AddDays(1)),
            storeIds,
            10,
            cancellationToken);

        return new SalesReport(
            query.FromDate,
            query.ToDate,
            query.StoreId,
            saleCount,
            grossRevenue,
            discount,
            revenue,
            saleCount == 0 ? 0 : Money(revenue / saleCount),
            daily,
            byStore,
            topProducts);
    }

    public async Task<PagedResult<InventoryReportRow>> GetInventoryAsync(
        Guid userId,
        InventoryReportQuery query,
        CancellationToken cancellationToken)
    {
        var descending = MasterDataPersistence.ValidateQuery(
            query.Page,
            query.PageSize,
            query.SortBy,
            InventorySorts,
            query.SortDirection);
        var storeIds = await GetScopedStoreIdsAsync(userId, query.StoreId, cancellationToken);
        var rows = dbContext.StockPositionReportRows
            .AsNoTracking()
            .Where(row => storeIds.Contains(row.StoreId));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(row =>
                row.ProductCode.Contains(search) ||
                row.ProductName.Contains(search) ||
                row.CategoryName.Contains(search));
        }

        if (query.BelowMinimum.HasValue)
        {
            rows = rows.Where(row => row.IsBelowMinimum == query.BelowMinimum.Value);
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        rows = (query.SortBy.ToLowerInvariant(), descending) switch
        {
            ("store", false) => rows.OrderBy(row => row.StoreName).ThenBy(row => row.ProductName),
            ("store", true) => rows.OrderByDescending(row => row.StoreName).ThenBy(row => row.ProductName),
            ("quantity", false) => rows.OrderBy(row => row.QuantityOnHand).ThenBy(row => row.ProductName),
            ("quantity", true) => rows.OrderByDescending(row => row.QuantityOnHand).ThenBy(row => row.ProductName),
            ("minimum", false) => rows.OrderBy(row => row.MinimumQuantity).ThenBy(row => row.ProductName),
            ("minimum", true) => rows.OrderByDescending(row => row.MinimumQuantity).ThenBy(row => row.ProductName),
            ("category", false) => rows.OrderBy(row => row.CategoryName).ThenBy(row => row.ProductName),
            ("category", true) => rows.OrderByDescending(row => row.CategoryName).ThenBy(row => row.ProductName),
            ("product", true) => rows.OrderByDescending(row => row.ProductName).ThenBy(row => row.StoreName),
            _ => rows.OrderBy(row => row.ProductName).ThenBy(row => row.StoreName)
        };

        var items = await rows
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new InventoryReportRow(
                row.StoreId,
                row.StoreCode,
                row.StoreName,
                row.ProductId,
                row.ProductCode,
                row.ProductName,
                row.CategoryName,
                row.QuantityOnHand,
                row.MinimumQuantity,
                row.IsBelowMinimum))
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryReportRow>(
            items,
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<MovementReport> GetMovementsAsync(
        Guid userId,
        MovementReportQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePeriod(query.FromDate, query.ToDate);
        var storeIds = await GetScopedStoreIdsAsync(userId, query.StoreId, cancellationToken);
        var fromUtc = UtcStart(query.FromDate);
        var toUtcExclusive = UtcStart(query.ToDate.AddDays(1));
        var movements = dbContext.StockMovements
            .AsNoTracking()
            .Where(movement =>
                storeIds.Contains(movement.StoreId) &&
                movement.OccurredAtUtc >= fromUtc &&
                movement.OccurredAtUtc < toUtcExclusive);
        var typeRows = await movements
            .GroupBy(movement => movement.Type)
            .Select(group => new
            {
                Type = group.Key,
                Count = group.LongCount(),
                Entries = group.Sum(movement => movement.QuantityChange > 0 ? movement.QuantityChange : 0),
                Exits = group.Sum(movement => movement.QuantityChange < 0 ? -movement.QuantityChange : 0)
            })
            .ToListAsync(cancellationToken);
        var storeRows = await movements
            .GroupBy(movement => new
            {
                movement.StoreId,
                movement.Store.Code,
                movement.Store.Name
            })
            .Select(group => new
            {
                group.Key.StoreId,
                StoreCode = group.Key.Code,
                StoreName = group.Key.Name,
                Count = group.LongCount(),
                Entries = group.Sum(movement => movement.QuantityChange > 0 ? movement.QuantityChange : 0),
                Exits = group.Sum(movement => movement.QuantityChange < 0 ? -movement.QuantityChange : 0)
            })
            .ToListAsync(cancellationToken);
        var byType = typeRows
            .OrderBy(row => row.Type)
            .Select(row => new MovementTypeSummary(row.Type.ToString(), row.Count, row.Entries, row.Exits))
            .ToArray();
        var byStore = storeRows
            .OrderBy(row => row.StoreName)
            .Select(row => new MovementStoreSummary(
                row.StoreId,
                row.StoreCode,
                row.StoreName,
                row.Count,
                row.Entries,
                row.Exits))
            .ToArray();

        return new MovementReport(
            query.FromDate,
            query.ToDate,
            query.StoreId,
            typeRows.Sum(row => row.Count),
            typeRows.Sum(row => row.Entries),
            typeRows.Sum(row => row.Exits),
            byType,
            byStore);
    }

    public async Task<PurchaseReport> GetPurchasesAsync(
        Guid userId,
        PurchaseReportQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePeriod(query.FromDate, query.ToDate);
        var storeIds = await GetScopedStoreIdsAsync(userId, query.StoreId, cancellationToken);
        var fromUtc = UtcStart(query.FromDate);
        var toUtcExclusive = UtcStart(query.ToDate.AddDays(1));
        var orders = dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(order =>
                storeIds.Contains(order.StoreId) &&
                order.CreatedAtUtc >= fromUtc &&
                order.CreatedAtUtc < toUtcExclusive);
        var orderRows = await orders
            .Select(order => new
            {
                order.Id,
                order.StoreId,
                StoreCode = order.Store.Code,
                StoreName = order.Store.Name,
                order.SupplierId,
                SupplierCode = order.Supplier.Code,
                SupplierName = order.Supplier.Name,
                order.Status,
                order.TotalAmount
            })
            .ToListAsync(cancellationToken);
        var orderIds = orderRows.Select(row => row.Id).ToArray();
        var receivedRows = await dbContext.PurchaseReceiptItems
            .AsNoTracking()
            .Where(item => orderIds.Contains(item.PurchaseOrderItem.PurchaseOrderId))
            .GroupBy(item => item.PurchaseOrderItem.PurchaseOrder.StoreId)
            .Select(group => new
            {
                StoreId = group.Key,
                Amount = group.Sum(item => item.Quantity * item.PurchaseOrderItem.UnitCost)
            })
            .ToListAsync(cancellationToken);
        var receivedByStore = receivedRows.ToDictionary(
            row => row.StoreId,
            row => Money(row.Amount));
        var byStatus = orderRows
            .GroupBy(row => row.Status)
            .OrderBy(group => group.Key)
            .Select(group => new PurchaseStatusSummary(
                group.Key.ToString(),
                group.LongCount(),
                Money(group.Sum(row => row.TotalAmount))))
            .ToArray();
        var byStore = orderRows
            .GroupBy(row => new { row.StoreId, row.StoreCode, row.StoreName })
            .OrderBy(group => group.Key.StoreName)
            .Select(group => new PurchaseStoreSummary(
                group.Key.StoreId,
                group.Key.StoreCode,
                group.Key.StoreName,
                group.LongCount(),
                Money(group.Where(row => row.Status != PurchaseOrderStatus.Cancelled).Sum(row => row.TotalAmount)),
                receivedByStore.GetValueOrDefault(group.Key.StoreId)))
            .ToArray();
        var bySupplier = orderRows
            .GroupBy(row => new { row.SupplierId, row.SupplierCode, row.SupplierName })
            .OrderByDescending(group => group
                .Where(row => row.Status != PurchaseOrderStatus.Cancelled)
                .Sum(row => row.TotalAmount))
            .ThenBy(group => group.Key.SupplierName)
            .Select(group => new PurchaseSupplierSummary(
                group.Key.SupplierId,
                group.Key.SupplierCode,
                group.Key.SupplierName,
                group.LongCount(),
                Money(group
                    .Where(row => row.Status != PurchaseOrderStatus.Cancelled)
                    .Sum(row => row.TotalAmount))))
            .ToArray();

        return new PurchaseReport(
            query.FromDate,
            query.ToDate,
            query.StoreId,
            orderRows.LongCount(),
            Money(orderRows
                .Where(row => row.Status != PurchaseOrderStatus.Cancelled)
                .Sum(row => row.TotalAmount)),
            Money(receivedRows.Sum(row => row.Amount)),
            byStatus,
            byStore,
            bySupplier);
    }

    private async Task<int[]> GetScopedStoreIdsAsync(
        Guid userId,
        int? requestedStoreId,
        CancellationToken cancellationToken)
    {
        if (requestedStoreId <= 0)
        {
            throw new RequestValidationException("storeId", "Informe uma loja válida.");
        }

        var isAdministrator = await (
            from userRole in dbContext.Set<IdentityUserRole<Guid>>()
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == userId && role.Name == RoleNames.Administrator
            select userRole).AnyAsync(cancellationToken);
        var accessibleStoreIds = isAdministrator
            ? await dbContext.Stores.AsNoTracking().Select(store => store.Id).ToArrayAsync(cancellationToken)
            : await dbContext.UserStoreRoles
                .AsNoTracking()
                .Where(assignment => assignment.UserId == userId && assignment.Store.IsActive)
                .Select(assignment => assignment.StoreId)
                .Distinct()
                .ToArrayAsync(cancellationToken);

        if (requestedStoreId.HasValue && !accessibleStoreIds.Contains(requestedStoreId.Value))
        {
            throw new OperationForbiddenException("O usuário não possui acesso à loja informada.");
        }

        return requestedStoreId.HasValue ? [requestedStoreId.Value] : accessibleStoreIds;
    }

    private async Task<IReadOnlyDictionary<int, (string Code, string Name)>> LoadStoresAsync(
        int[] storeIds,
        CancellationToken cancellationToken) =>
        await dbContext.Stores
            .AsNoTracking()
            .Where(store => storeIds.Contains(store.Id))
            .ToDictionaryAsync(store => store.Id, store => ValueTuple.Create(store.Code, store.Name), cancellationToken);

    private async Task<IReadOnlyCollection<TopSellingProductSummary>> LoadTopSellingProductsAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        int[] storeIds,
        int limit,
        CancellationToken cancellationToken)
    {
        if (storeIds.Length == 0)
        {
            return [];
        }

        var parameters = new object[]
        {
            new SqlParameter("@FromUtc", SqlDbType.DateTime2) { Value = fromUtc },
            new SqlParameter("@ToUtcExclusive", SqlDbType.DateTime2) { Value = toUtcExclusive },
            new SqlParameter("@StoreIds", SqlDbType.NVarChar, -1) { Value = string.Join(',', storeIds) },
            new SqlParameter("@Limit", SqlDbType.Int) { Value = limit }
        };
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "reporting.usp_TopSellingProducts";
            command.CommandType = CommandType.StoredProcedure;
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            var products = new List<TopSellingProductSummary>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                products.Add(new TopSellingProductSummary(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDecimal(3),
                    Money(reader.GetDecimal(4)),
                    reader.GetInt64(5)));
            }

            return products;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void ValidatePeriod(DateOnly fromDate, DateOnly toDate)
    {
        if (fromDate > toDate)
        {
            throw new RequestValidationException("fromDate", "A data inicial não pode ser posterior à data final.");
        }

        if (toDate.DayNumber - fromDate.DayNumber >= MaximumPeriodDays)
        {
            throw new RequestValidationException(
                "toDate",
                $"O período deve possuir no máximo {MaximumPeriodDays} dias.");
        }
    }

    private static DateTime UtcStart(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
