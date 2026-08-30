using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreControl.Domain.Inventory;
using StoreControl.Infrastructure.Persistence;
using StoreControl.IntegrationTests.Support;

namespace StoreControl.IntegrationTests.Purchasing;

public sealed class PurchaseTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task Creating_purchase_does_not_change_stock()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetPurchaseContextAsync(client, 2);
        var quantitiesBefore = await GetQuantitiesAsync(context.StoreId, context.Products.Select(item => item.Id));

        var created = await CreatePurchaseAsync(client, context);

        Assert.Equal("Created", created.GetProperty("status").GetString());
        var quantitiesAfter = await GetQuantitiesAsync(context.StoreId, context.Products.Select(item => item.Id));
        Assert.Equal(quantitiesBefore, quantitiesAfter);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var orderId = created.GetProperty("id").GetInt64();
        Assert.Empty(await dbContext.PurchaseReceipts
            .AsNoTracking()
            .Where(receipt => receipt.PurchaseOrderId == orderId)
            .ToListAsync());
        Assert.DoesNotContain(await dbContext.StockMovements.AsNoTracking().ToListAsync(), movement =>
            movement.ReferenceType == "PurchaseOrder" && movement.ReferenceId == orderId.ToString());
    }

    [Fact]
    public async Task Receipt_updates_stock_and_duplicate_is_idempotent()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetPurchaseContextAsync(client, 1);
        var created = await CreatePurchaseAsync(client, context);
        var orderId = created.GetProperty("id").GetInt64();
        var submitted = await TransitionAsync(client, orderId, "submit", created.GetProperty("rowVersion").GetString()!);
        var item = submitted.GetProperty("items")[0];
        var quantityBefore = (await GetQuantitiesAsync(context.StoreId, [context.Products[0].Id]))[context.Products[0].Id];
        var receiptId = Guid.NewGuid();
        var body = new
        {
            rowVersion = submitted.GetProperty("rowVersion").GetString(),
            notes = "Recebimento idempotente de integração",
            items = new[]
            {
                new
                {
                    purchaseOrderItemId = item.GetProperty("id").GetInt64(),
                    quantity = item.GetProperty("orderedQuantity").GetDecimal()
                }
            }
        };

        var firstResponse = await ReceiveAsync(client, orderId, receiptId, body);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.False(firstResult!.RootElement.GetProperty("wasAlreadyProcessed").GetBoolean());
        Assert.Equal("Received", firstResult.RootElement.GetProperty("purchase").GetProperty("status").GetString());
        var quantityAfterFirst = (await GetQuantitiesAsync(context.StoreId, [context.Products[0].Id]))[context.Products[0].Id];
        Assert.Equal(quantityBefore + item.GetProperty("orderedQuantity").GetDecimal(), quantityAfterFirst);

        var replayResponse = await ReceiveAsync(client, orderId, receiptId, body);

        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var replay = await replayResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(replay!.RootElement.GetProperty("wasAlreadyProcessed").GetBoolean());
        Assert.Equal(quantityAfterFirst, (await GetQuantitiesAsync(context.StoreId, [context.Products[0].Id]))[context.Products[0].Id]);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        Assert.Equal(1, await dbContext.PurchaseReceipts.CountAsync(receipt => receipt.Id == receiptId));
        Assert.Equal(1, await dbContext.StockMovements.CountAsync(movement =>
            movement.ReferenceType == "PurchaseReceipt" && movement.ReferenceId == receiptId.ToString("D")));

        var finalized = await TransitionAsync(
            client,
            orderId,
            "finalize",
            replay.RootElement.GetProperty("purchase").GetProperty("rowVersion").GetString()!);
        Assert.Equal("Finalized", finalized.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Receipt_persistence_failure_rolls_back_order_and_every_stock_change()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetPurchaseContextAsync(client, 2);
        var created = await CreatePurchaseAsync(client, context);
        var orderId = created.GetProperty("id").GetInt64();
        var submitted = await TransitionAsync(client, orderId, "submit", created.GetProperty("rowVersion").GetString()!);
        var receiptId = Guid.NewGuid();
        var userId = await GetAdministratorIdAsync();

        await using (var arrangeScope = factory.Services.CreateAsyncScope())
        {
            var arrangeDb = arrangeScope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
            var conflictingProductId = context.Products[1].Id;
            var balance = await arrangeDb.StockBalances.SingleAsync(item =>
                item.StoreId == context.StoreId && item.ProductId == conflictingProductId);
            arrangeDb.StockMovements.Add(balance.ApplyMovement(
                StockMovementType.PositiveAdjustment,
                1m,
                "Conflito controlado para validar rollback.",
                userId,
                $"test-{receiptId:N}",
                "PurchaseReceipt",
                receiptId.ToString("D")));
            await arrangeDb.SaveChangesAsync();
        }

        var quantitiesBefore = await GetQuantitiesAsync(context.StoreId, context.Products.Select(item => item.Id));
        var items = submitted.GetProperty("items").EnumerateArray()
            .Select(item => new
            {
                purchaseOrderItemId = item.GetProperty("id").GetInt64(),
                quantity = item.GetProperty("orderedQuantity").GetDecimal()
            })
            .ToArray();

        var failed = await ReceiveAsync(client, orderId, receiptId, new
        {
            rowVersion = submitted.GetProperty("rowVersion").GetString(),
            notes = "Este recebimento deve sofrer rollback",
            items
        });

        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
        Assert.Equal(quantitiesBefore, await GetQuantitiesAsync(context.StoreId, context.Products.Select(item => item.Id)));
        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var persistedOrder = await assertDb.PurchaseOrders
            .AsNoTracking()
            .Include(item => item.Items)
            .SingleAsync(item => item.Id == orderId);
        Assert.Equal(Domain.Purchasing.PurchaseOrderStatus.Pending, persistedOrder.Status);
        Assert.All(persistedOrder.Items, item => Assert.Equal(0m, item.ReceivedQuantity));
        Assert.Equal(0, await assertDb.PurchaseReceipts.CountAsync(receipt => receipt.Id == receiptId));
        Assert.Equal(1, await assertDb.StockMovements.CountAsync(movement =>
            movement.ReferenceType == "PurchaseReceipt" && movement.ReferenceId == receiptId.ToString("D")));
    }

    [Fact]
    public async Task Invalid_status_is_rejected_and_pending_purchase_can_be_cancelled()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetPurchaseContextAsync(client, 1);
        var created = await CreatePurchaseAsync(client, context);
        var orderId = created.GetProperty("id").GetInt64();
        var item = created.GetProperty("items")[0];
        var invalidReceipt = await ReceiveAsync(client, orderId, Guid.NewGuid(), new
        {
            rowVersion = created.GetProperty("rowVersion").GetString(),
            items = new[]
            {
                new
                {
                    purchaseOrderItemId = item.GetProperty("id").GetInt64(),
                    quantity = 1m
                }
            }
        });
        Assert.Equal(HttpStatusCode.Conflict, invalidReceipt.StatusCode);

        var submitted = await TransitionAsync(client, orderId, "submit", created.GetProperty("rowVersion").GetString()!);
        var invalidSubmit = await client.PostAsJsonAsync(
            $"/api/purchases/{orderId}/submit",
            new { rowVersion = submitted.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Conflict, invalidSubmit.StatusCode);

        var cancelledResponse = await client.PostAsJsonAsync($"/api/purchases/{orderId}/cancel", new
        {
            rowVersion = submitted.GetProperty("rowVersion").GetString(),
            reason = "Fornecedor não atenderá ao pedido."
        });
        Assert.Equal(HttpStatusCode.OK, cancelledResponse.StatusCode);
        var cancelled = await cancelledResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("Cancelled", cancelled!.RootElement.GetProperty("status").GetString());

        var cancelledReceipt = await ReceiveAsync(client, orderId, Guid.NewGuid(), new
        {
            rowVersion = cancelled.RootElement.GetProperty("rowVersion").GetString(),
            items = new[]
            {
                new
                {
                    purchaseOrderItemId = item.GetProperty("id").GetInt64(),
                    quantity = 1m
                }
            }
        });
        Assert.Equal(HttpStatusCode.Conflict, cancelledReceipt.StatusCode);
    }

    [Fact]
    public async Task Permissions_and_store_scope_are_enforced_by_the_api()
    {
        using var seller = await CreateAuthenticatedClientAsync(
            "vendedor@storecontrol.local",
            SqlServerApiFactory.UserPassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync("/api/purchases")).StatusCode);

        using var administrator = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetPurchaseContextAsync(administrator, 1, "SUL");
        using var stockClerk = await CreateAuthenticatedClientAsync(
            "estoquista@storecontrol.local",
            SqlServerApiFactory.UserPassword);
        var outOfScope = await stockClerk.PostAsJsonAsync("/api/purchases", new
        {
            storeId = context.StoreId,
            supplierId = context.SupplierId,
            notes = "Loja fora do escopo do estoquista",
            items = context.Products.Select(item => new
            {
                productId = item.Id,
                quantity = 1m,
                unitCost = item.CostPrice
            })
        });
        Assert.Equal(HttpStatusCode.Forbidden, outOfScope.StatusCode);
    }

    private async Task<JsonElement> CreatePurchaseAsync(HttpClient client, PurchaseContext context)
    {
        var response = await client.PostAsJsonAsync("/api/purchases", new
        {
            storeId = context.StoreId,
            supplierId = context.SupplierId,
            expectedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            notes = $"Pedido de integração {Guid.NewGuid():N}",
            items = context.Products.Select((item, index) => new
            {
                productId = item.Id,
                quantity = 2m + index,
                unitCost = item.CostPrice
            })
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return payload!.RootElement.Clone();
    }

    private static async Task<JsonElement> TransitionAsync(
        HttpClient client,
        long orderId,
        string action,
        string rowVersion)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/purchases/{orderId}/{action}",
            new { rowVersion });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return payload!.RootElement.Clone();
    }

    private static Task<HttpResponseMessage> ReceiveAsync(
        HttpClient client,
        long orderId,
        Guid receiptId,
        object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/purchases/{orderId}/receipts")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Idempotency-Key", receiptId.ToString("D"));
        return client.SendAsync(request);
    }

    private async Task<PurchaseContext> GetPurchaseContextAsync(
        HttpClient client,
        int productCount,
        string storeCode = "CENTRAL")
    {
        var stores = await client.GetFromJsonAsync<JsonDocument>("/api/stores");
        var storeId = stores!.RootElement.EnumerateArray()
            .Single(store => store.GetProperty("code").GetString() == storeCode)
            .GetProperty("id").GetInt32();
        var suppliers = await client.GetFromJsonAsync<JsonDocument>("/api/suppliers?page=1&pageSize=1&isActive=true");
        var supplierId = suppliers!.RootElement.GetProperty("items")[0].GetProperty("id").GetInt32();
        var products = await client.GetFromJsonAsync<JsonDocument>($"/api/products?page=1&pageSize={productCount}&isActive=true");
        var selectedProducts = products!.RootElement.GetProperty("items").EnumerateArray()
            .Select(product => new ProductContext(
                product.GetProperty("id").GetInt32(),
                product.GetProperty("costPrice").GetDecimal()))
            .ToArray();
        Assert.Equal(productCount, selectedProducts.Length);
        return new PurchaseContext(storeId, supplierId, selectedProducts);
    }

    private async Task<Dictionary<int, decimal>> GetQuantitiesAsync(
        int storeId,
        IEnumerable<int> productIds)
    {
        var ids = productIds.ToArray();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        return await dbContext.StockBalances
            .AsNoTracking()
            .Where(balance => balance.StoreId == storeId && ids.Contains(balance.ProductId))
            .ToDictionaryAsync(balance => balance.ProductId, balance => balance.QuantityOnHand);
    }

    private async Task<Guid> GetAdministratorIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        return await (
            from userRole in dbContext.Set<IdentityUserRole<Guid>>()
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where role.Name == Domain.Security.RoleNames.Administrator
            select userRole.UserId).FirstAsync();
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

    private sealed record PurchaseContext(int StoreId, int SupplierId, ProductContext[] Products);

    private sealed record ProductContext(int Id, decimal CostPrice);
}
