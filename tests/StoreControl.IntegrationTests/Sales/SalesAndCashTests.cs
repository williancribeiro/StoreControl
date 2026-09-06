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
using StoreControl.Infrastructure.Messaging;
using StoreControl.IntegrationTests.Support;

namespace StoreControl.IntegrationTests.Sales;

public sealed class SalesAndCashTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task Sale_finalizes_multiple_items_and_payments_atomically_and_replays_idempotently()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetSaleContextAsync(client, "CENTRAL", 2);
        var session = await GetOrOpenSessionAsync(client, context.CashRegisterId);
        var quantitiesBefore = await GetQuantitiesAsync(context.StoreId, context.Products.Select(item => item.Id));
        var expectedCashBefore = session.GetProperty("expectedCashAmount").GetDecimal();
        var subtotal = decimal.Round(
            context.Products[0].SalePrice + (context.Products[1].SalePrice * 2m),
            2,
            MidpointRounding.AwayFromZero);
        var discount = 0.01m;
        var total = subtotal - discount;
        var cashAmount = total - 1m;
        var operationId = Guid.NewGuid();
        var body = new
        {
            storeId = context.StoreId,
            cashSessionId = session.GetProperty("id").GetInt64(),
            customerId = (int?)null,
            discountAmount = discount,
            items = new[]
            {
                new { productId = context.Products[0].Id, quantity = 1m },
                new { productId = context.Products[1].Id, quantity = 2m }
            },
            payments = new object[]
            {
                new { method = "Cash", amount = cashAmount, reference = (string?)null },
                new { method = "Pix", amount = 1m, reference = "PIX-INTEGRATION" }
            }
        };

        var response = await SendWithIdempotencyAsync(client, "/api/sales", operationId, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var result = payload!.RootElement;
        Assert.False(result.GetProperty("wasAlreadyProcessed").GetBoolean());
        var sale = result.GetProperty("sale");
        Assert.Equal(subtotal, sale.GetProperty("subtotal").GetDecimal());
        Assert.Equal(total, sale.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(2, sale.GetProperty("items").GetArrayLength());
        Assert.Equal(2, sale.GetProperty("payments").GetArrayLength());
        var historicalItems = sale.GetProperty("items").EnumerateArray()
            .ToDictionary(item => item.GetProperty("productId").GetInt32());
        Assert.Equal(context.Products[0].Code, historicalItems[context.Products[0].Id].GetProperty("productCode").GetString());
        Assert.Equal(context.Products[0].SalePrice, historicalItems[context.Products[0].Id].GetProperty("unitPrice").GetDecimal());
        Assert.Equal(context.Products[1].SalePrice, historicalItems[context.Products[1].Id].GetProperty("unitPrice").GetDecimal());

        var quantitiesAfter = await GetQuantitiesAsync(context.StoreId, context.Products.Select(item => item.Id));
        Assert.Equal(quantitiesBefore[context.Products[0].Id] - 1m, quantitiesAfter[context.Products[0].Id]);
        Assert.Equal(quantitiesBefore[context.Products[1].Id] - 2m, quantitiesAfter[context.Products[1].Id]);
        var currentSession = await GetCurrentSessionAsync(client, context.CashRegisterId);
        Assert.Equal(expectedCashBefore + cashAmount, currentSession.GetProperty("expectedCashAmount").GetDecimal());

        var replayResponse = await SendWithIdempotencyAsync(client, "/api/sales", operationId, body);

        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var replay = await replayResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(replay!.RootElement.GetProperty("wasAlreadyProcessed").GetBoolean());
        Assert.Equal(quantitiesAfter, await GetQuantitiesAsync(context.StoreId, context.Products.Select(item => item.Id)));
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        Assert.Equal(1, await dbContext.Sales.CountAsync(item => item.OperationId == operationId));
        Assert.Equal(2, await dbContext.StockMovements.CountAsync(item =>
            item.ReferenceType == "Sale" && item.ReferenceId == operationId.ToString("D")));
        Assert.Equal(2, await dbContext.CashMovements.CountAsync(item =>
            item.ReferenceType == "Sale" && item.ReferenceId == operationId.ToString("D")));
        var outbox = await dbContext.OutboxMessages.SingleAsync(item => item.Id == operationId);
        Assert.Equal(MessagingContracts.SaleFinalizedType, outbox.Type);
        Assert.Equal(MessagingContracts.SaleFinalizedRoutingKey, outbox.RoutingKey);
        var integrationEvent = MessagingContracts.DeserializeSaleFinalized(
            System.Text.Encoding.UTF8.GetBytes(outbox.Payload));
        Assert.Equal(sale.GetProperty("id").GetInt64(), integrationEvent.SaleId);
        Assert.Equal(total, integrationEvent.TotalAmount);

        var eventBody = System.Text.Encoding.UTF8.GetBytes(outbox.Payload);
        var processor = scope.ServiceProvider.GetRequiredService<SaleFinalizedProcessor>();
        Assert.Equal(
            SaleFinalizedProcessingOutcome.SkippedNoDestination,
            await processor.ProcessAsync(operationId, eventBody, default));
        Assert.Equal(
            SaleFinalizedProcessingOutcome.Duplicate,
            await processor.ProcessAsync(operationId, eventBody, default));
        Assert.Equal(1, await dbContext.InboxMessages.CountAsync(item => item.MessageId == operationId));
        Assert.Equal(1, await dbContext.SaleReceiptDeliveries.CountAsync(item => item.MessageId == operationId));

        var conflictingEvent = integrationEvent with { TotalAmount = integrationEvent.TotalAmount + 0.01m };
        var conflictingBody = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            conflictingEvent,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await Assert.ThrowsAsync<MessageIdentityCollisionException>(() =>
            processor.ProcessAsync(operationId, conflictingBody, default));
        Assert.Equal(1, await dbContext.SaleReceiptDeliveries.CountAsync(item => item.MessageId == operationId));
    }

    [Fact]
    public async Task Insufficient_stock_rejects_sale_without_sale_cash_or_stock_side_effects()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetSaleContextAsync(client, "NORTE", 1);
        var session = await GetOrOpenSessionAsync(client, context.CashRegisterId);
        var product = context.Products[0];
        var quantityBefore = (await GetQuantitiesAsync(context.StoreId, [product.Id]))[product.Id];
        var cashBefore = session.GetProperty("expectedCashAmount").GetDecimal();
        var operationId = Guid.NewGuid();

        var response = await SendSaleAsync(
            client,
            operationId,
            context.StoreId,
            session.GetProperty("id").GetInt64(),
            product.Id,
            quantityBefore + 1m,
            product.SalePrice * (quantityBefore + 1m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(quantityBefore, (await GetQuantitiesAsync(context.StoreId, [product.Id]))[product.Id]);
        Assert.Equal(cashBefore, (await GetCurrentSessionAsync(client, context.CashRegisterId))
            .GetProperty("expectedCashAmount").GetDecimal());
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        Assert.Equal(0, await dbContext.Sales.CountAsync(item => item.OperationId == operationId));
        Assert.Equal(0, await dbContext.StockMovements.CountAsync(item =>
            item.ReferenceType == "Sale" && item.ReferenceId == operationId.ToString("D")));
        Assert.Equal(0, await dbContext.OutboxMessages.CountAsync(item => item.Id == operationId));
    }

    [Fact]
    public async Task Two_concurrent_sales_of_last_item_allow_exactly_one_to_finish()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetSaleContextAsync(client, "SUL", 1);
        var session = await GetOrOpenSessionAsync(client, context.CashRegisterId);
        var product = context.Products[0];
        await SetStockToOneAsync(context.StoreId, product.Id);
        var sessionId = session.GetProperty("id").GetInt64();

        var first = SendSaleAsync(
            client, Guid.NewGuid(), context.StoreId, sessionId, product.Id, 1m, product.SalePrice);
        var second = SendSaleAsync(
            client, Guid.NewGuid(), context.StoreId, sessionId, product.Id, 1m, product.SalePrice);
        var responses = await Task.WhenAll(first, second);

        Assert.Single(responses, item => item.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, item => item.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(0m, (await GetQuantitiesAsync(context.StoreId, [product.Id]))[product.Id]);
    }

    [Fact]
    public async Task Persistence_failure_rolls_back_sale_every_stock_change_and_cash()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetSaleContextAsync(client, "CENTRAL", 2);
        var session = await GetOrOpenSessionAsync(client, context.CashRegisterId);
        var operationId = Guid.NewGuid();
        var userId = await GetAdministratorIdAsync();
        await using (var arrangeScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = arrangeScope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
            var balance = await dbContext.StockBalances.SingleAsync(item =>
                item.StoreId == context.StoreId && item.ProductId == context.Products[1].Id);
            dbContext.StockMovements.Add(balance.ApplyMovement(
                StockMovementType.PositiveAdjustment,
                1m,
                "Conflito controlado da venda.",
                userId,
                $"test-{operationId:N}",
                "Sale",
                operationId.ToString("D")));
            await dbContext.SaveChangesAsync();
        }

        var quantitiesBefore = await GetQuantitiesAsync(context.StoreId, context.Products.Select(item => item.Id));
        var cashBefore = (await GetCurrentSessionAsync(client, context.CashRegisterId))
            .GetProperty("expectedCashAmount").GetDecimal();
        var total = context.Products.Sum(item => item.SalePrice);
        var body = new
        {
            storeId = context.StoreId,
            cashSessionId = session.GetProperty("id").GetInt64(),
            discountAmount = 0m,
            items = context.Products.Select(item => new { productId = item.Id, quantity = 1m }),
            payments = new[] { new { method = "Cash", amount = total, reference = (string?)null } }
        };

        var response = await SendWithIdempotencyAsync(client, "/api/sales", operationId, body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(quantitiesBefore, await GetQuantitiesAsync(context.StoreId, context.Products.Select(item => item.Id)));
        Assert.Equal(cashBefore, (await GetCurrentSessionAsync(client, context.CashRegisterId))
            .GetProperty("expectedCashAmount").GetDecimal());
        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        Assert.Equal(0, await assertDb.Sales.CountAsync(item => item.OperationId == operationId));
        Assert.Equal(1, await assertDb.StockMovements.CountAsync(item =>
            item.ReferenceType == "Sale" && item.ReferenceId == operationId.ToString("D")));
        Assert.Equal(0, await assertDb.CashMovements.CountAsync(item =>
            item.ReferenceType == "Sale" && item.ReferenceId == operationId.ToString("D")));
        Assert.Equal(0, await assertDb.OutboxMessages.CountAsync(item => item.Id == operationId));
    }

    [Fact]
    public async Task Cancellation_restores_stock_refunds_cash_preserves_sale_and_replays_idempotently()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetSaleContextAsync(client, "CENTRAL", 1);
        var session = await GetOrOpenSessionAsync(client, context.CashRegisterId);
        var product = context.Products[0];
        var quantityBefore = (await GetQuantitiesAsync(context.StoreId, [product.Id]))[product.Id];
        var expectedCashBefore = session.GetProperty("expectedCashAmount").GetDecimal();
        var saleResponse = await SendSaleAsync(
            client,
            Guid.NewGuid(),
            context.StoreId,
            session.GetProperty("id").GetInt64(),
            product.Id,
            1m,
            product.SalePrice);
        Assert.Equal(HttpStatusCode.Created, saleResponse.StatusCode);
        var saleDocument = await saleResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var sale = saleDocument!.RootElement.GetProperty("sale").Clone();
        var cancellationId = Guid.NewGuid();
        var cancellationBody = new
        {
            cashSessionId = session.GetProperty("id").GetInt64(),
            rowVersion = sale.GetProperty("rowVersion").GetString(),
            reason = "Cliente desistiu antes de retirar a mercadoria."
        };

        var response = await SendWithIdempotencyAsync(
            client,
            $"/api/sales/{sale.GetProperty("id").GetInt64()}/cancellation",
            cancellationId,
            cancellationBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var result = payload!.RootElement;
        Assert.False(result.GetProperty("wasAlreadyProcessed").GetBoolean());
        var cancelled = result.GetProperty("sale");
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal(2, cancelled.GetProperty("statusHistory").GetArrayLength());
        Assert.Equal(quantityBefore, (await GetQuantitiesAsync(context.StoreId, [product.Id]))[product.Id]);
        Assert.Equal(expectedCashBefore, (await GetCurrentSessionAsync(client, context.CashRegisterId))
            .GetProperty("expectedCashAmount").GetDecimal());

        var replayResponse = await SendWithIdempotencyAsync(
            client,
            $"/api/sales/{sale.GetProperty("id").GetInt64()}/cancellation",
            cancellationId,
            cancellationBody);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var replay = await replayResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(replay!.RootElement.GetProperty("wasAlreadyProcessed").GetBoolean());

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        Assert.Equal(1, await dbContext.Sales.CountAsync(item =>
            item.Id == sale.GetProperty("id").GetInt64() &&
            item.Status == StoreControl.Domain.Sales.SaleStatus.Cancelled));
        Assert.Equal(1, await dbContext.StockMovements.CountAsync(item =>
            item.ReferenceType == "SaleCancellation" && item.ReferenceId == cancellationId.ToString("D")));
        Assert.Equal(1, await dbContext.CashMovements.CountAsync(item =>
            item.ReferenceType == "SaleCancellation" && item.ReferenceId == cancellationId.ToString("D")));
    }

    [Fact]
    public async Task Failed_cash_refund_rolls_back_sale_status_and_stock_reversal()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetSaleContextAsync(client, "NORTE", 1);
        var session = await GetOrOpenSessionAsync(client, context.CashRegisterId);
        var product = context.Products[0];
        var saleResponse = await SendSaleAsync(
            client,
            Guid.NewGuid(),
            context.StoreId,
            session.GetProperty("id").GetInt64(),
            product.Id,
            1m,
            product.SalePrice);
        Assert.Equal(HttpStatusCode.Created, saleResponse.StatusCode);
        var saleDocument = await saleResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var sale = saleDocument!.RootElement.GetProperty("sale").Clone();
        var stockAfterSale = (await GetQuantitiesAsync(context.StoreId, [product.Id]))[product.Id];
        var currentSession = await GetCurrentSessionAsync(client, context.CashRegisterId);
        currentSession = await AddCashMovementAsync(
            client,
            currentSession,
            "CashOut",
            currentSession.GetProperty("expectedCashAmount").GetDecimal(),
            "Retirada total para testar rollback do estorno");
        var cancellationId = Guid.NewGuid();

        var response = await SendWithIdempotencyAsync(
            client,
            $"/api/sales/{sale.GetProperty("id").GetInt64()}/cancellation",
            cancellationId,
            new
            {
                cashSessionId = currentSession.GetProperty("id").GetInt64(),
                rowVersion = sale.GetProperty("rowVersion").GetString(),
                reason = "Cancelamento que exige numerário indisponível."
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var persisted = await client.GetFromJsonAsync<JsonDocument>(
            $"/api/sales/{sale.GetProperty("id").GetInt64()}");
        Assert.Equal("Finalized", persisted!.RootElement.GetProperty("status").GetString());
        Assert.Equal(stockAfterSale, (await GetQuantitiesAsync(context.StoreId, [product.Id]))[product.Id]);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        Assert.Equal(0, await dbContext.StockMovements.CountAsync(item =>
            item.ReferenceType == "SaleCancellation" && item.ReferenceId == cancellationId.ToString("D")));
        Assert.Equal(0, await dbContext.CashMovements.CountAsync(item =>
            item.ReferenceType == "SaleCancellation" && item.ReferenceId == cancellationId.ToString("D")));
    }

    [Fact]
    public async Task Cash_session_supports_idempotent_open_signed_manual_movements_and_close()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetSaleContextAsync(client, "NORTE", 1);
        var existing = await TryGetCurrentSessionAsync(client, context.CashRegisterId);
        if (existing.HasValue)
        {
            await CloseSessionAsync(client, existing.Value, existing.Value.GetProperty("expectedCashAmount").GetDecimal());
        }

        var operationId = Guid.NewGuid();
        var openBody = new { cashRegisterId = context.CashRegisterId, openingAmount = 100m };
        var openedResponse = await SendWithIdempotencyAsync(client, "/api/cash/sessions", operationId, openBody);
        Assert.Equal(HttpStatusCode.Created, openedResponse.StatusCode);
        var openedPayload = await openedResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var opened = openedPayload!.RootElement.GetProperty("session").Clone();
        var replayResponse = await SendWithIdempotencyAsync(client, "/api/cash/sessions", operationId, openBody);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);

        var cashIn = await AddCashMovementAsync(client, opened, "CashIn", 20m, "Reforço de troco");
        Assert.Equal(120m, cashIn.GetProperty("expectedCashAmount").GetDecimal());
        var cashOut = await AddCashMovementAsync(client, cashIn, "CashOut", 15m, "Sangria operacional");
        Assert.Equal(105m, cashOut.GetProperty("expectedCashAmount").GetDecimal());

        var closed = await CloseSessionAsync(client, cashOut, 104m);

        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.Equal(-1m, closed.GetProperty("differenceAmount").GetDecimal());
        Assert.Equal(3, closed.GetProperty("movements").GetArrayLength());
    }

    [Fact]
    public async Task Sales_and_cash_permissions_and_store_scope_are_enforced()
    {
        using var stockClerk = await CreateAuthenticatedClientAsync(
            "estoquista@storecontrol.local",
            SqlServerApiFactory.UserPassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await stockClerk.GetAsync("/api/sales")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stockClerk.GetAsync("/api/cash/registers")).StatusCode);

        using var seller = await CreateAuthenticatedClientAsync(
            "vendedor@storecontrol.local",
            SqlServerApiFactory.UserPassword);
        Assert.Equal(HttpStatusCode.OK, (await seller.GetAsync("/api/sales")).StatusCode);
        var registers = await seller.GetFromJsonAsync<JsonDocument>("/api/cash/registers");
        Assert.NotEmpty(registers!.RootElement.EnumerateArray());
        Assert.DoesNotContain(registers.RootElement.EnumerateArray(), item =>
            item.GetProperty("storeCode").GetString() == "SUL");
    }

    private async Task<SaleContext> GetSaleContextAsync(HttpClient client, string storeCode, int productCount)
    {
        var stores = await client.GetFromJsonAsync<JsonDocument>("/api/stores");
        var storeId = stores!.RootElement.EnumerateArray()
            .Single(store => store.GetProperty("code").GetString() == storeCode)
            .GetProperty("id").GetInt32();
        var registers = await client.GetFromJsonAsync<JsonDocument>($"/api/cash/registers?storeId={storeId}");
        var cashRegisterId = registers!.RootElement[0].GetProperty("id").GetInt32();
        var products = await client.GetFromJsonAsync<JsonDocument>($"/api/products?page=1&pageSize={productCount}&isActive=true&sortBy=code&sortDirection=asc");
        var selected = products!.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => new ProductContext(
                item.GetProperty("id").GetInt32(),
                item.GetProperty("code").GetString()!,
                item.GetProperty("salePrice").GetDecimal()))
            .ToArray();
        Assert.Equal(productCount, selected.Length);
        return new SaleContext(storeId, cashRegisterId, selected);
    }

    private async Task<JsonElement> GetOrOpenSessionAsync(HttpClient client, int cashRegisterId)
    {
        var existing = await TryGetCurrentSessionAsync(client, cashRegisterId);
        if (existing.HasValue)
        {
            return existing.Value;
        }

        var response = await SendWithIdempotencyAsync(
            client,
            "/api/cash/sessions",
            Guid.NewGuid(),
            new { cashRegisterId, openingAmount = 100m });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return payload!.RootElement.GetProperty("session").Clone();
    }

    private async Task<JsonElement?> TryGetCurrentSessionAsync(HttpClient client, int cashRegisterId)
    {
        var response = await client.GetAsync($"/api/cash/sessions/current?cashRegisterId={cashRegisterId}");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return payload!.RootElement.Clone();
    }

    private async Task<JsonElement> GetCurrentSessionAsync(HttpClient client, int cashRegisterId) =>
        (await TryGetCurrentSessionAsync(client, cashRegisterId))!.Value;

    private static async Task<JsonElement> AddCashMovementAsync(
        HttpClient client,
        JsonElement session,
        string type,
        decimal amount,
        string reason)
    {
        var response = await SendWithIdempotencyAsync(
            client,
            $"/api/cash/sessions/{session.GetProperty("id").GetInt64()}/movements",
            Guid.NewGuid(),
            new { rowVersion = session.GetProperty("rowVersion").GetString(), type, amount, reason });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return payload!.RootElement.GetProperty("session").Clone();
    }

    private static async Task<JsonElement> CloseSessionAsync(
        HttpClient client,
        JsonElement session,
        decimal declaredAmount)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/cash/sessions/{session.GetProperty("id").GetInt64()}/close",
            new { rowVersion = session.GetProperty("rowVersion").GetString(), declaredClosingAmount = declaredAmount });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return payload!.RootElement.Clone();
    }

    private static Task<HttpResponseMessage> SendSaleAsync(
        HttpClient client,
        Guid operationId,
        int storeId,
        long cashSessionId,
        int productId,
        decimal quantity,
        decimal amount) =>
        SendWithIdempotencyAsync(client, "/api/sales", operationId, new
        {
            storeId,
            cashSessionId,
            discountAmount = 0m,
            items = new[] { new { productId, quantity } },
            payments = new[] { new { method = "Cash", amount, reference = (string?)null } }
        });

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

    private async Task SetStockToOneAsync(int storeId, int productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var balance = await dbContext.StockBalances.SingleAsync(item =>
            item.StoreId == storeId && item.ProductId == productId);
        var difference = balance.QuantityOnHand - 1m;
        if (difference > 0)
        {
            var userId = await GetAdministratorIdAsync(dbContext);
            dbContext.StockMovements.Add(balance.ApplyMovement(
                StockMovementType.NegativeAdjustment,
                difference,
                "Preparação do cenário concorrente.",
                userId,
                $"test-{Guid.NewGuid():N}"));
            await dbContext.SaveChangesAsync();
        }

        Assert.Equal(1m, balance.QuantityOnHand);
    }

    private async Task<Dictionary<int, decimal>> GetQuantitiesAsync(int storeId, IEnumerable<int> productIds)
    {
        var ids = productIds.ToArray();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        return await dbContext.StockBalances.AsNoTracking()
            .Where(item => item.StoreId == storeId && ids.Contains(item.ProductId))
            .ToDictionaryAsync(item => item.ProductId, item => item.QuantityOnHand);
    }

    private async Task<Guid> GetAdministratorIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await GetAdministratorIdAsync(scope.ServiceProvider.GetRequiredService<StoreControlDbContext>());
    }

    private static Task<Guid> GetAdministratorIdAsync(StoreControlDbContext dbContext) =>
        (from userRole in dbContext.Set<IdentityUserRole<Guid>>()
         join role in dbContext.Roles on userRole.RoleId equals role.Id
         where role.Name == RoleNames.Administrator
         select userRole.UserId).FirstAsync();

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

    private sealed record SaleContext(int StoreId, int CashRegisterId, ProductContext[] Products);

    private sealed record ProductContext(int Id, string Code, decimal SalePrice);
}
