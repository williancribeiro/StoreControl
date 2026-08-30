using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreControl.Domain.Inventory;
using StoreControl.Domain.Security;
using StoreControl.Infrastructure.Persistence;
using StoreControl.IntegrationTests.Support;

namespace StoreControl.IntegrationTests.Inventory;

public sealed class StockTransferTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task Transfer_creation_dispatch_and_receipt_move_stock_only_at_each_transition()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetContextAsync(client, "CENTRAL", "NORTE");
        var originBefore = await GetQuantityAsync(context.OriginStoreId, context.ProductId);
        var destinationBefore = await GetQuantityAsync(context.DestinationStoreId, context.ProductId);
        var createOperationId = Guid.NewGuid();
        var createBody = new
        {
            originStoreId = context.OriginStoreId,
            destinationStoreId = context.DestinationStoreId,
            notes = "Reposição entre filiais",
            items = new[] { new { productId = context.ProductId, quantity = 1m } }
        };

        var createResponse = await SendWithIdempotencyAsync(
            client,
            "/api/inventory/transfers",
            createOperationId,
            createBody);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createDocument = await createResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var created = createDocument!.RootElement.GetProperty("transfer").Clone();
        Assert.Equal("Created", created.GetProperty("status").GetString());
        Assert.Equal(originBefore, await GetQuantityAsync(context.OriginStoreId, context.ProductId));
        Assert.Equal(destinationBefore, await GetQuantityAsync(context.DestinationStoreId, context.ProductId));

        var createReplay = await SendWithIdempotencyAsync(
            client,
            "/api/inventory/transfers",
            createOperationId,
            createBody);
        Assert.Equal(HttpStatusCode.OK, createReplay.StatusCode);

        var dispatchOperationId = Guid.NewGuid();
        var dispatchBody = new { rowVersion = created.GetProperty("rowVersion").GetString() };
        var dispatchResponse = await SendWithIdempotencyAsync(
            client,
            $"/api/inventory/transfers/{created.GetProperty("id").GetInt64()}/dispatch",
            dispatchOperationId,
            dispatchBody);
        Assert.Equal(HttpStatusCode.OK, dispatchResponse.StatusCode);
        var dispatchDocument = await dispatchResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var dispatched = dispatchDocument!.RootElement.GetProperty("transfer").Clone();
        Assert.Equal("InTransit", dispatched.GetProperty("status").GetString());
        Assert.Equal(originBefore - 1m, await GetQuantityAsync(context.OriginStoreId, context.ProductId));
        Assert.Equal(destinationBefore, await GetQuantityAsync(context.DestinationStoreId, context.ProductId));

        var dispatchReplay = await SendWithIdempotencyAsync(
            client,
            $"/api/inventory/transfers/{created.GetProperty("id").GetInt64()}/dispatch",
            dispatchOperationId,
            dispatchBody);
        Assert.Equal(HttpStatusCode.OK, dispatchReplay.StatusCode);
        var dispatchReplayDocument = await dispatchReplay.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(dispatchReplayDocument!.RootElement.GetProperty("wasAlreadyProcessed").GetBoolean());

        var receiptOperationId = Guid.NewGuid();
        var receiptBody = new { rowVersion = dispatched.GetProperty("rowVersion").GetString() };
        var receiptResponse = await SendWithIdempotencyAsync(
            client,
            $"/api/inventory/transfers/{created.GetProperty("id").GetInt64()}/receive",
            receiptOperationId,
            receiptBody);
        Assert.Equal(HttpStatusCode.OK, receiptResponse.StatusCode);
        var receiptDocument = await receiptResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var received = receiptDocument!.RootElement.GetProperty("transfer");
        Assert.Equal("Received", received.GetProperty("status").GetString());
        Assert.Equal(3, received.GetProperty("statusHistory").GetArrayLength());
        Assert.Equal(destinationBefore + 1m, await GetQuantityAsync(context.DestinationStoreId, context.ProductId));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        Assert.Equal(1, await dbContext.StockMovements.CountAsync(item =>
            item.ReferenceType == "StockTransferDispatch" &&
            item.ReferenceId == dispatchOperationId.ToString("D")));
        Assert.Equal(1, await dbContext.StockMovements.CountAsync(item =>
            item.ReferenceType == "StockTransferReceipt" &&
            item.ReferenceId == receiptOperationId.ToString("D")));
    }

    [Fact]
    public async Task Created_transfer_can_be_cancelled_without_stock_effect_and_cannot_be_dispatched()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetContextAsync(client, "NORTE", "SUL");
        var originBefore = await GetQuantityAsync(context.OriginStoreId, context.ProductId);
        var destinationBefore = await GetQuantityAsync(context.DestinationStoreId, context.ProductId);
        var created = await CreateTransferAsync(client, context, 1m);
        var cancellationId = Guid.NewGuid();
        var cancelResponse = await SendWithIdempotencyAsync(
            client,
            $"/api/inventory/transfers/{created.GetProperty("id").GetInt64()}/cancel",
            cancellationId,
            new
            {
                rowVersion = created.GetProperty("rowVersion").GetString(),
                reason = "Solicitação duplicada."
            });

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelDocument = await cancelResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var cancelled = cancelDocument!.RootElement.GetProperty("transfer");
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal(originBefore, await GetQuantityAsync(context.OriginStoreId, context.ProductId));
        Assert.Equal(destinationBefore, await GetQuantityAsync(context.DestinationStoreId, context.ProductId));

        var dispatchResponse = await SendWithIdempotencyAsync(
            client,
            $"/api/inventory/transfers/{created.GetProperty("id").GetInt64()}/dispatch",
            Guid.NewGuid(),
            new { rowVersion = cancelled.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Conflict, dispatchResponse.StatusCode);
    }

    [Fact]
    public async Task Concurrent_transfers_of_last_unit_allow_exactly_one_dispatch()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetContextAsync(client, "SUL", "CENTRAL");
        await SetStockToOneAsync(context.OriginStoreId, context.ProductId);
        var firstTransfer = await CreateTransferAsync(client, context, 1m);
        var secondTransfer = await CreateTransferAsync(client, context, 1m);

        var first = SendWithIdempotencyAsync(
            client,
            $"/api/inventory/transfers/{firstTransfer.GetProperty("id").GetInt64()}/dispatch",
            Guid.NewGuid(),
            new { rowVersion = firstTransfer.GetProperty("rowVersion").GetString() });
        var second = SendWithIdempotencyAsync(
            client,
            $"/api/inventory/transfers/{secondTransfer.GetProperty("id").GetInt64()}/dispatch",
            Guid.NewGuid(),
            new { rowVersion = secondTransfer.GetProperty("rowVersion").GetString() });
        var responses = await Task.WhenAll(first, second);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(0m, await GetQuantityAsync(context.OriginStoreId, context.ProductId));
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var transferIds = new[]
        {
            firstTransfer.GetProperty("id").GetInt64(),
            secondTransfer.GetProperty("id").GetInt64()
        };
        Assert.Equal(1, await dbContext.StockTransfers.CountAsync(transfer =>
            transferIds.Contains(transfer.Id) && transfer.Status == StockTransferStatus.InTransit));
    }

    [Fact]
    public async Task Transfer_write_requires_inventory_manage_and_access_to_both_stores()
    {
        using var administrator = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetContextAsync(administrator, "CENTRAL", "NORTE");
        var body = new
        {
            originStoreId = context.OriginStoreId,
            destinationStoreId = context.DestinationStoreId,
            items = new[] { new { productId = context.ProductId, quantity = 1m } }
        };

        using var seller = await CreateAuthenticatedClientAsync(
            "vendedor@storecontrol.local",
            SqlServerApiFactory.UserPassword);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendWithIdempotencyAsync(
                seller,
                "/api/inventory/transfers",
                Guid.NewGuid(),
                body)).StatusCode);

        using var manager = await CreateAuthenticatedClientAsync(
            "gerente@storecontrol.local",
            SqlServerApiFactory.UserPassword);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendWithIdempotencyAsync(
                manager,
                "/api/inventory/transfers",
                Guid.NewGuid(),
                body)).StatusCode);
    }

    private async Task<TransferContext> GetContextAsync(
        HttpClient client,
        string originStoreCode,
        string destinationStoreCode)
    {
        var stores = await client.GetFromJsonAsync<JsonDocument>("/api/stores");
        var storeItems = stores!.RootElement.EnumerateArray().ToArray();
        var originStoreId = storeItems.Single(item =>
            item.GetProperty("code").GetString() == originStoreCode).GetProperty("id").GetInt32();
        var destinationStoreId = storeItems.Single(item =>
            item.GetProperty("code").GetString() == destinationStoreCode).GetProperty("id").GetInt32();
        var products = await client.GetFromJsonAsync<JsonDocument>(
            "/api/products?page=1&pageSize=1&isActive=true&sortBy=code&sortDirection=asc");
        var productId = products!.RootElement.GetProperty("items")[0].GetProperty("id").GetInt32();
        return new TransferContext(originStoreId, destinationStoreId, productId);
    }

    private async Task<JsonElement> CreateTransferAsync(
        HttpClient client,
        TransferContext context,
        decimal quantity)
    {
        var response = await SendWithIdempotencyAsync(
            client,
            "/api/inventory/transfers",
            Guid.NewGuid(),
            new
            {
                originStoreId = context.OriginStoreId,
                destinationStoreId = context.DestinationStoreId,
                items = new[] { new { productId = context.ProductId, quantity } }
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return document!.RootElement.GetProperty("transfer").Clone();
    }

    private async Task<decimal> GetQuantityAsync(int storeId, int productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        return await dbContext.StockBalances
            .AsNoTracking()
            .Where(balance => balance.StoreId == storeId && balance.ProductId == productId)
            .Select(balance => balance.QuantityOnHand)
            .SingleOrDefaultAsync();
    }

    private async Task SetStockToOneAsync(int storeId, int productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var balance = await dbContext.StockBalances.SingleAsync(item =>
            item.StoreId == storeId && item.ProductId == productId);
        var userId = await (
            from userRole in dbContext.Set<IdentityUserRole<Guid>>()
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where role.Name == RoleNames.Administrator
            select userRole.UserId).FirstAsync();
        if (balance.QuantityOnHand > 1m)
        {
            dbContext.StockMovements.Add(balance.ApplyMovement(
                StockMovementType.NegativeAdjustment,
                balance.QuantityOnHand - 1m,
                "Preparação do teste concorrente de transferência.",
                userId,
                $"test-{Guid.NewGuid():N}"));
        }
        else if (balance.QuantityOnHand < 1m)
        {
            dbContext.StockMovements.Add(balance.ApplyMovement(
                StockMovementType.PositiveAdjustment,
                1m - balance.QuantityOnHand,
                "Preparação do teste concorrente de transferência.",
                userId,
                $"test-{Guid.NewGuid():N}"));
        }

        await dbContext.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> SendWithIdempotencyAsync(
        HttpClient client,
        string uri,
        Guid operationId,
        object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", operationId.ToString("D"));
        return client.SendAsync(request);
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

    private sealed record TransferContext(int OriginStoreId, int DestinationStoreId, int ProductId);
}
