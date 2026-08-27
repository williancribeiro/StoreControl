using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StoreControl.Infrastructure.Persistence;

public sealed class StoreControlDbContextFactory : IDesignTimeDbContextFactory<StoreControlDbContext>
{
    public StoreControlDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__StoreControl") ??
            Environment.GetEnvironmentVariable("STORECONTROL_DB_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Defina ConnectionStrings__StoreControl ou STORECONTROL_DB_CONNECTION para usar as ferramentas do EF Core.");
        }

        var options = new DbContextOptionsBuilder<StoreControlDbContext>()
            .UseSqlServer(connectionString, sqlServer =>
                sqlServer.MigrationsAssembly(typeof(StoreControlDbContext).Assembly.FullName))
            .Options;

        return new StoreControlDbContext(options);
    }
}
