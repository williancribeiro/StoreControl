using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreControl.Domain.Catalog;
using StoreControl.Infrastructure.Persistence;
using StoreControl.IntegrationTests.Support;

namespace StoreControl.IntegrationTests.Caching;

public sealed class CacheGenerationTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task Category_generation_changes_with_commit_but_rolls_back_with_data()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var original = await GenerationAsync(db, "categories");
        var dashboard = await GenerationAsync(db, "dashboard");
        var category = new Category("CACHE-GEN", "Cache generation", null);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var committed = await GenerationAsync(db, "categories");
        Assert.NotEqual(original, committed);
        Assert.Equal(dashboard, await GenerationAsync(db, "dashboard"));
        Assert.NotEmpty(category.RowVersion);

        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [catalog].[Categories] SET [Name] = N'Rolled back' WHERE [Id] = {category.Id}");
            Assert.NotEqual(committed, await GenerationAsync(db, "categories"));
            await transaction.RollbackAsync();
        });
        Assert.Equal(committed, await GenerationAsync(db, "categories"));
        Assert.Equal("Cache generation", await db.Categories.AsNoTracking()
            .Where(item => item.Id == category.Id).Select(item => item.Name).SingleAsync());
    }

    [Theory]
    [InlineData("UPDATE TOP (1) [catalog].[Products] SET [Name] = [Name]")]
    [InlineData("UPDATE TOP (1) [organization].[Stores] SET [Name] = [Name]")]
    [InlineData("UPDATE TOP (1) [inventory].[StockBalances] SET [MinimumQuantity] = [MinimumQuantity]")]
    public async Task Dashboard_dependencies_invalidate_even_for_direct_SQL(string sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var original = await GenerationAsync(db, "dashboard");
        await db.Database.ExecuteSqlRawAsync(sql);
        Assert.NotEqual(original, await GenerationAsync(db, "dashboard"));
    }

    private static Task<Guid> GenerationAsync(StoreControlDbContext db, string region) =>
        db.Database.SqlQuery<Guid>(
            $"SELECT [Generation] AS [Value] FROM [platform].[CacheRevisions] WHERE [Name] = {region}")
            .SingleAsync();
}
