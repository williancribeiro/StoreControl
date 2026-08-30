using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StoreControl.Domain.Purchasing;
using StoreControl.Domain.Security;
using StoreControl.Infrastructure.Persistence.Models;

namespace StoreControl.Infrastructure.Persistence.Seeding;

internal sealed class PurchaseSeeder(
    StoreControlDbContext dbContext,
    ILogger<PurchaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        const string seedKey = "demo-purchases-v1";
        if (await dbContext.FoundationRecords.AnyAsync(record => record.Key == seedKey, cancellationToken))
        {
            logger.LogInformation("Demonstration purchase seed is already current");
            return;
        }

        var administratorId = await (
            from userRole in dbContext.Set<IdentityUserRole<Guid>>()
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where role.Name == RoleNames.Administrator
            select userRole.UserId).FirstAsync(cancellationToken);
        var stores = await dbContext.Stores
            .AsNoTracking()
            .Where(store => store.IsActive)
            .OrderBy(store => store.Id)
            .ToArrayAsync(cancellationToken);
        var suppliers = await dbContext.Suppliers
            .AsNoTracking()
            .Where(supplier => supplier.IsActive)
            .OrderBy(supplier => supplier.Id)
            .Take(8)
            .ToArrayAsync(cancellationToken);
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .OrderBy(product => product.Id)
            .Take(32)
            .ToArrayAsync(cancellationToken);

        if (stores.Length == 0 || suppliers.Length == 0 || products.Length < 3)
        {
            throw new InvalidOperationException(
                "O seed de compras requer lojas, fornecedores e ao menos três produtos ativos.");
        }

        var orders = Enumerable.Range(0, 8).Select(index =>
        {
            var order = new PurchaseOrder(
                stores[index % stores.Length].Id,
                suppliers[index % suppliers.Length].Id,
                administratorId,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(index + 2)),
                $"Pedido demonstrativo {index + 1:00}.");
            foreach (var product in products.Skip(index * 3).Take(3))
            {
                order.AddItem(
                    product.Id,
                    product.Code,
                    product.Name,
                    5m + (product.Id % 7),
                    product.CostPrice);
            }

            if (index >= 2)
            {
                order.Submit(administratorId);
            }

            return order;
        }).ToArray();

        dbContext.PurchaseOrders.AddRange(orders);
        dbContext.FoundationRecords.Add(new FoundationRecord
        {
            Key = seedKey,
            Value = "8 orders: 2 created, 6 pending",
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created {PurchaseOrderCount} demonstration purchase orders", orders.Length);
    }
}
