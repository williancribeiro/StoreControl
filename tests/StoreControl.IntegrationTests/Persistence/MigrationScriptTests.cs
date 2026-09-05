using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using StoreControl.Infrastructure.Persistence;
using StoreControl.IntegrationTests.Support;

namespace StoreControl.IntegrationTests.Persistence;

public sealed class MigrationScriptTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task Idempotent_script_executes_from_zero_twice_and_cache_migration_can_be_reverted_and_reapplied()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var original = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var connectionString = new SqlConnectionStringBuilder(original.Database.GetConnectionString())
        {
            InitialCatalog = $"StoreControl_IntegrationTests_Script_{Guid.NewGuid():N}"
        };
        var options = new DbContextOptionsBuilder<StoreControlDbContext>().UseSqlServer(connectionString.ConnectionString).Options;
        await using var db = new StoreControlDbContext(options);
        try
        {
            await db.GetService<IRelationalDatabaseCreator>().CreateAsync();
            var migrator = db.GetService<IMigrator>();
            var script = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.OpenConnectionAsync();
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var batch in Regex.Split(script, @"^GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    await using var command = db.Database.GetDbConnection().CreateCommand();
                    command.CommandText = batch;
                    command.CommandTimeout = 60;
                    await command.ExecuteNonQueryAsync();
                }
            }

            Assert.Equal(db.Database.GetMigrations().Count(), (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(2, await db.CacheRevisions.CountAsync());
            await db.Database.CloseConnectionAsync();
            await migrator.MigrateAsync("20260903024210_OptimizeReportingQueries");
            var triggers = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM sys.triggers WHERE [name] LIKE 'TR_%_CacheRevision'").SingleAsync();
            Assert.Equal(0, triggers);
            await migrator.MigrateAsync();
            Assert.Equal(2, await db.CacheRevisions.CountAsync());
        }
        finally
        {
            if (!connectionString.InitialCatalog.StartsWith("StoreControl_IntegrationTests_Script_", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to delete a database outside the script-test prefix.");
            await db.Database.EnsureDeletedAsync();
        }
    }
}
