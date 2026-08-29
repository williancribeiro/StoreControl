using Microsoft.EntityFrameworkCore;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.IntegrationTests.Persistence;

public sealed class MigrationMetadataTests
{
    [Fact]
    public void Required_migrations_are_discoverable()
    {
        var options = new DbContextOptionsBuilder<StoreControlDbContext>()
            .UseSqlServer("Server=localhost;Database=StoreControl_Metadata_Test;Integrated Security=true;TrustServerCertificate=true")
            .Options;

        using var dbContext = new StoreControlDbContext(options);

        var migrations = dbContext.Database.GetMigrations().ToArray();

        Assert.Contains(migrations, migration => migration.EndsWith("_InitialFoundation", StringComparison.Ordinal));
        Assert.Contains(migrations, migration => migration.EndsWith("_AuthenticationAndStores", StringComparison.Ordinal));
        Assert.Contains(migrations, migration => migration.EndsWith("_MainRegistrations", StringComparison.Ordinal));
        Assert.Contains(migrations, migration => migration.EndsWith("_Inventory", StringComparison.Ordinal));
    }
}
