using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreControl.Infrastructure.Persistence;
using StoreControl.IntegrationTests.Support;

namespace StoreControl.IntegrationTests.Inventory;

public sealed class InventoryTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task Adjustment_is_traceable_atomic_and_protected_by_store_scope()
    {
        using var administrator = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var stores = await administrator.GetFromJsonAsync<JsonDocument>("/api/stores");
        var centralId = stores!.RootElement.EnumerateArray()
            .Single(store => store.GetProperty("code").GetString() == "CENTRAL")
            .GetProperty("id").GetInt32();
        var southId = stores.RootElement.EnumerateArray()
            .Single(store => store.GetProperty("code").GetString() == "SUL")
            .GetProperty("id").GetInt32();
        var initial = await GetFirstPositionAsync(administrator, centralId);
        var productId = initial.GetProperty("productId").GetInt32();
        var initialQuantity = initial.GetProperty("quantityOnHand").GetDecimal();
        var reason = $"Ajuste rastreável {Guid.NewGuid():N}";

        var adjustment = await administrator.PostAsJsonAsync("/api/inventory/adjustments", new
        {
            storeId = centralId,
            productId,
            direction = "positive",
            quantity = 2.3455m,
            reason,
            rowVersion = initial.GetProperty("rowVersion").GetString()
        });

        Assert.Equal(HttpStatusCode.Created, adjustment.StatusCode);
        var adjusted = await adjustment.Content.ReadFromJsonAsync<JsonDocument>();
        var position = adjusted!.RootElement.GetProperty("position");
        var movement = adjusted.RootElement.GetProperty("movement");
        Assert.Equal(initialQuantity + 2.346m, position.GetProperty("quantityOnHand").GetDecimal());
        Assert.Equal(2.346m, movement.GetProperty("quantityChange").GetDecimal());
        Assert.Equal(initialQuantity, movement.GetProperty("quantityBefore").GetDecimal());
        Assert.Equal(position.GetProperty("quantityOnHand").GetDecimal(), movement.GetProperty("quantityAfter").GetDecimal());
        Assert.Equal(reason, movement.GetProperty("reason").GetString());
        Assert.False(string.IsNullOrWhiteSpace(movement.GetProperty("correlationId").GetString()));

        var failedReason = $"Sem movimento {Guid.NewGuid():N}";
        var insufficient = await administrator.PostAsJsonAsync("/api/inventory/adjustments", new
        {
            storeId = centralId,
            productId,
            direction = "negative",
            quantity = position.GetProperty("quantityOnHand").GetDecimal() + 1m,
            reason = failedReason,
            rowVersion = position.GetProperty("rowVersion").GetString()
        });
        Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode);

        var afterFailure = await administrator.GetFromJsonAsync<JsonDocument>(
            $"/api/inventory/balances/{centralId}/{productId}");
        Assert.Equal(
            position.GetProperty("quantityOnHand").GetDecimal(),
            afterFailure!.RootElement.GetProperty("quantityOnHand").GetDecimal());
        var missingMovement = await administrator.GetFromJsonAsync<JsonDocument>(
            $"/api/inventory/movements?search={Uri.EscapeDataString(failedReason)}");
        Assert.Equal(0, missingMovement!.RootElement.GetProperty("totalCount").GetInt32());

        using var seller = await CreateAuthenticatedClientAsync(
            "vendedor@storecontrol.local",
            SqlServerApiFactory.UserPassword);
        var sellerRead = await seller.GetAsync($"/api/inventory/balances?storeId={centralId}");
        Assert.Equal(HttpStatusCode.OK, sellerRead.StatusCode);
        var sellerWrite = await seller.PostAsJsonAsync("/api/inventory/adjustments", new
        {
            storeId = centralId,
            productId,
            direction = "positive",
            quantity = 1m,
            reason = "Ajuste sem permissão",
            rowVersion = afterFailure.RootElement.GetProperty("rowVersion").GetString()
        });
        Assert.Equal(HttpStatusCode.Forbidden, sellerWrite.StatusCode);

        using var stockClerk = await CreateAuthenticatedClientAsync(
            "estoquista@storecontrol.local",
            SqlServerApiFactory.UserPassword);
        var outOfScope = await stockClerk.PostAsJsonAsync("/api/inventory/adjustments", new
        {
            storeId = southId,
            productId,
            direction = "positive",
            quantity = 1m,
            reason = "Loja fora do escopo",
            rowVersion = (string?)null
        });
        Assert.Equal(HttpStatusCode.Forbidden, outOfScope.StatusCode);
    }

    [Fact]
    public async Task Minimum_and_adjustment_reject_stale_rowversion()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var initial = await GetFirstPositionAsync(client);
        var storeId = initial.GetProperty("storeId").GetInt32();
        var productId = initial.GetProperty("productId").GetInt32();
        var staleVersion = initial.GetProperty("rowVersion").GetString();

        var updated = await client.PutAsJsonAsync(
            $"/api/inventory/balances/{storeId}/{productId}/minimum",
            new { minimumQuantity = 7.1235m, rowVersion = staleVersion });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var current = await updated.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(7.124m, current!.RootElement.GetProperty("minimumQuantity").GetDecimal());

        var staleMinimum = await client.PutAsJsonAsync(
            $"/api/inventory/balances/{storeId}/{productId}/minimum",
            new { minimumQuantity = 8m, rowVersion = staleVersion });
        Assert.Equal(HttpStatusCode.Conflict, staleMinimum.StatusCode);

        var staleAdjustment = await client.PostAsJsonAsync("/api/inventory/adjustments", new
        {
            storeId,
            productId,
            direction = "positive",
            quantity = 1m,
            reason = "Versão obsoleta",
            rowVersion = staleVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, staleAdjustment.StatusCode);
    }

    [Fact]
    public async Task Concurrent_negative_adjustments_allow_exactly_one_writer_and_one_movement()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var initial = await GetFirstPositionAsync(client);
        var storeId = initial.GetProperty("storeId").GetInt32();
        var productId = initial.GetProperty("productId").GetInt32();
        var quantityBefore = initial.GetProperty("quantityOnHand").GetDecimal();
        var rowVersion = initial.GetProperty("rowVersion").GetString();
        var reason = $"Concorrência {Guid.NewGuid():N}";
        var request = new
        {
            storeId,
            productId,
            direction = "negative",
            quantity = 1m,
            reason,
            rowVersion
        };

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/inventory/adjustments", request),
            client.PostAsJsonAsync("/api/inventory/adjustments", request));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var current = await client.GetFromJsonAsync<JsonDocument>(
            $"/api/inventory/balances/{storeId}/{productId}");
        Assert.Equal(quantityBefore - 1m, current!.RootElement.GetProperty("quantityOnHand").GetDecimal());
        var movements = await client.GetFromJsonAsync<JsonDocument>(
            $"/api/inventory/movements?search={Uri.EscapeDataString(reason)}");
        Assert.Equal(1, movements!.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Seeded_ledger_matches_balance_and_database_rejects_negative_direct_update()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var balance = await dbContext.StockBalances.AsNoTracking().FirstAsync();
        var ledgerTotal = await dbContext.StockMovements
            .Where(movement =>
                movement.StoreId == balance.StoreId && movement.ProductId == balance.ProductId)
            .SumAsync(movement => movement.QuantityChange);

        Assert.Equal(balance.QuantityOnHand, ledgerTotal);
        await Assert.ThrowsAsync<SqlException>(() => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [inventory].[StockBalances] SET [QuantityOnHand] = {-1m} WHERE [StoreId] = {balance.StoreId} AND [ProductId] = {balance.ProductId}"));
    }

    [Fact]
    public async Task New_product_has_zero_positions_and_first_adjustment_creates_the_tracked_balance()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var categories = await client.GetFromJsonAsync<JsonDocument>("/api/categories?page=1&pageSize=1");
        var categoryId = categories!.RootElement.GetProperty("items")[0].GetProperty("id").GetInt32();
        var uniqueCode = $"INV-{Guid.NewGuid():N}"[..20];
        var createdProductResponse = await client.PostAsJsonAsync("/api/products", new
        {
            code = uniqueCode,
            name = "Produto criado durante teste de estoque",
            categoryId,
            costPrice = 10m,
            salePrice = 15m
        });
        createdProductResponse.EnsureSuccessStatusCode();
        var createdProduct = await createdProductResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var productId = createdProduct!.RootElement.GetProperty("id").GetInt32();

        var zeroPositions = await client.GetFromJsonAsync<JsonDocument>(
            $"/api/inventory/balances?productId={productId}&pageSize=10");
        Assert.Equal(3, zeroPositions!.RootElement.GetProperty("totalCount").GetInt32());
        Assert.All(
            zeroPositions.RootElement.GetProperty("items").EnumerateArray(),
            position =>
            {
                Assert.Equal(0m, position.GetProperty("quantityOnHand").GetDecimal());
                Assert.Equal(JsonValueKind.Null, position.GetProperty("rowVersion").ValueKind);
            });

        var firstPosition = zeroPositions.RootElement.GetProperty("items")[0];
        var adjustment = await client.PostAsJsonAsync("/api/inventory/adjustments", new
        {
            storeId = firstPosition.GetProperty("storeId").GetInt32(),
            productId,
            direction = "positive",
            quantity = 3m,
            reason = "Primeira entrada rastreada",
            rowVersion = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, adjustment.StatusCode);
        var result = await adjustment.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(0m, result!.RootElement.GetProperty("movement").GetProperty("quantityBefore").GetDecimal());
        Assert.Equal(3m, result.RootElement.GetProperty("position").GetProperty("quantityOnHand").GetDecimal());
        Assert.Equal(JsonValueKind.String, result.RootElement.GetProperty("position").GetProperty("rowVersion").ValueKind);
    }

    private async Task<JsonElement> GetFirstPositionAsync(HttpClient client, int? storeId = null)
    {
        var query = storeId.HasValue
            ? $"/api/inventory/balances?page=1&pageSize=1&storeId={storeId.Value}"
            : "/api/inventory/balances?page=1&pageSize=1";
        var response = await client.GetFromJsonAsync<JsonDocument>(query);
        Assert.True(response!.RootElement.GetProperty("totalCount").GetInt32() >= (storeId.HasValue ? 120 : 360));
        return response.RootElement.GetProperty("items")[0].Clone();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var payload = await login.Content.ReadFromJsonAsync<JsonDocument>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            payload!.RootElement.GetProperty("accessToken").GetString());
        return client;
    }
}
