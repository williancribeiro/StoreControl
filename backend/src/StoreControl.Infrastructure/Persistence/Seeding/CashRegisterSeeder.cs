using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StoreControl.Domain.Cash;

namespace StoreControl.Infrastructure.Persistence.Seeding;

internal sealed class CashRegisterSeeder(
    StoreControlDbContext dbContext,
    ILogger<CashRegisterSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var stores = await dbContext.Stores
            .AsNoTracking()
            .OrderBy(store => store.Id)
            .ToArrayAsync(cancellationToken);
        var existingStoreIds = await dbContext.CashRegisters
            .AsNoTracking()
            .Where(register => register.Code == "CX-01")
            .Select(register => register.StoreId)
            .ToListAsync(cancellationToken);
        var missing = stores
            .Where(store => !existingStoreIds.Contains(store.Id))
            .Select(store => new CashRegister(store.Id, "CX-01", "Caixa principal"))
            .ToArray();
        if (missing.Length == 0)
        {
            logger.LogInformation("Reference cash registers are already current");
            return;
        }

        dbContext.CashRegisters.AddRange(missing);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created {CashRegisterCount} reference cash registers", missing.Length);
    }
}
