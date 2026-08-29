using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StoreControl.Domain.Inventory;
using StoreControl.Domain.Security;

namespace StoreControl.Infrastructure.Persistence.Seeding;

internal sealed class InventorySeeder(
    StoreControlDbContext dbContext,
    ILogger<InventorySeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var administratorId = await (
            from userRole in dbContext.Set<IdentityUserRole<Guid>>()
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where role.Name == RoleNames.Administrator
            select userRole.UserId).FirstAsync(cancellationToken);
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .OrderBy(product => product.Id)
            .ToListAsync(cancellationToken);
        var stores = await dbContext.Stores
            .AsNoTracking()
            .Where(store => store.IsActive)
            .OrderBy(store => store.Id)
            .ToListAsync(cancellationToken);
        var existingKeys = (await dbContext.StockBalances
            .AsNoTracking()
            .Select(balance => new { balance.StoreId, balance.ProductId })
            .ToListAsync(cancellationToken))
            .Select(item => (item.StoreId, item.ProductId))
            .ToHashSet();

        var created = 0;
        foreach (var store in stores)
        {
            foreach (var product in products)
            {
                if (existingKeys.Contains((store.Id, product.Id)))
                {
                    continue;
                }

                var minimum = 4m + (product.Id % 9);
                var quantity = product.Id % 10 == 0
                    ? Math.Max(1m, minimum - 1m)
                    : minimum + ((product.Id * 7 + store.Id * 11) % 45);
                var balance = new StockBalance(store.Id, product.Id, minimum);
                var movement = balance.ApplyMovement(
                    StockMovementType.InitialBalance,
                    quantity,
                    "Saldo inicial demonstrativo.",
                    administratorId,
                    "demo-inventory-seed-v1",
                    "DemoSeed",
                    "inventory-v1");
                dbContext.StockBalances.Add(balance);
                dbContext.StockMovements.Add(movement);
                created++;
            }
        }

        if (created == 0)
        {
            logger.LogInformation("Demonstration inventory seed is already current");
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created {StockBalanceCount} demonstration stock balances with initial movements", created);
    }
}
