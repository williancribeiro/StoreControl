using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreControl.Infrastructure.Persistence;
using StoreControl.IntegrationTests.Support;

namespace StoreControl.IntegrationTests.Reporting;

public sealed class ReportingTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task Dashboard_and_sales_report_use_committed_sales_and_exclude_cancellations()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var context = await GetSalesContextAsync(client, "CENTRAL", 2);
        var session = await GetOrOpenSessionAsync(client, context.CashRegisterId);
        var includedSale = await CreateSaleAsync(
            client,
            context.StoreId,
            session.GetProperty("id").GetInt64(),
            context.Products[0],
            2m);
        var cancelledSale = await CreateSaleAsync(
            client,
            context.StoreId,
            session.GetProperty("id").GetInt64(),
            context.Products[1],
            1m);
        var cancellation = await SendWithIdempotencyAsync(
            client,
            $"/api/sales/{cancelledSale.GetProperty("id").GetInt64()}/cancellation",
            Guid.NewGuid(),
            new
            {
                cashSessionId = session.GetProperty("id").GetInt64(),
                rowVersion = cancelledSale.GetProperty("rowVersion").GetString(),
                reason = "Cancelamento para validar os relatórios."
            });
        cancellation.EnsureSuccessStatusCode();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var date = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var dashboard = await client.GetFromJsonAsync<JsonDocument>(
            $"/api/dashboard?storeId={context.StoreId}&date={date}");
        var dashboardRoot = dashboard!.RootElement;
        var expectedRevenue = includedSale.GetProperty("totalAmount").GetDecimal();

        Assert.Equal(1, dashboardRoot.GetProperty("salesCount").GetInt64());
        Assert.Equal(expectedRevenue, dashboardRoot.GetProperty("revenue").GetDecimal());
        Assert.Equal(expectedRevenue, dashboardRoot.GetProperty("averageTicket").GetDecimal());
        Assert.Equal(7, dashboardRoot.GetProperty("salesEvolution").GetArrayLength());
        var topProduct = Assert.Single(dashboardRoot.GetProperty("topSellingProducts").EnumerateArray());
        Assert.Equal(context.Products[0].Id, topProduct.GetProperty("productId").GetInt32());
        Assert.Equal(2m, topProduct.GetProperty("quantitySold").GetDecimal());

        var report = await client.GetFromJsonAsync<JsonDocument>(
            $"/api/reports/sales?fromDate={date}&toDate={date}&storeId={context.StoreId}");
        var reportRoot = report!.RootElement;
        Assert.Equal(1, reportRoot.GetProperty("saleCount").GetInt64());
        Assert.Equal(expectedRevenue, reportRoot.GetProperty("revenue").GetDecimal());
        Assert.Equal(1, reportRoot.GetProperty("byStore").GetArrayLength());
        Assert.Single(reportRoot.GetProperty("topSellingProducts").EnumerateArray());
    }

    [Fact]
    public async Task Reports_enforce_permission_store_scope_and_period_validation()
    {
        using var manager = await CreateAuthenticatedClientAsync(
            "gerente@storecontrol.local",
            SqlServerApiFactory.UserPassword);
        var inventory = await manager.GetFromJsonAsync<JsonDocument>(
            "/api/reports/inventory?page=1&pageSize=100&sortBy=store&sortDirection=asc");
        var inventoryItems = inventory!.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.NotEmpty(inventoryItems);
        Assert.All(inventoryItems, item => Assert.Equal("CENTRAL", item.GetProperty("storeCode").GetString()));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var toDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var movements = await manager.GetFromJsonAsync<JsonDocument>(
            $"/api/reports/movements?fromDate={fromDate}&toDate={toDate}");
        Assert.NotEmpty(movements!.RootElement.GetProperty("byType").EnumerateArray());
        Assert.All(
            movements.RootElement.GetProperty("byStore").EnumerateArray(),
            item => Assert.Equal("CENTRAL", item.GetProperty("storeCode").GetString()));

        var purchases = await manager.GetFromJsonAsync<JsonDocument>(
            $"/api/reports/purchases?fromDate={fromDate}&toDate={toDate}");
        Assert.True(purchases!.RootElement.GetProperty("orderCount").GetInt64() > 0);
        Assert.All(
            purchases.RootElement.GetProperty("byStore").EnumerateArray(),
            item => Assert.Equal("CENTRAL", item.GetProperty("storeCode").GetString()));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var southStoreId = await dbContext.Stores
            .Where(store => store.Code == "SUL")
            .Select(store => store.Id)
            .SingleAsync();
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await manager.GetAsync($"/api/dashboard?storeId={southStoreId}")).StatusCode);

        using var stockClerk = await CreateAuthenticatedClientAsync(
            "estoquista@storecontrol.local",
            SqlServerApiFactory.UserPassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await stockClerk.GetAsync("/api/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stockClerk.GetAsync("/api/reports/inventory")).StatusCode);

        var excessiveFrom = today.AddDays(-366).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await manager.GetAsync(
                $"/api/reports/sales?fromDate={excessiveFrom}&toDate={toDate}")).StatusCode);
    }

    [Fact]
    public async Task Reporting_migration_creates_query_assets_and_supporting_indexes()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var assetCount = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS [Value]
                FROM sys.objects
                WHERE SCHEMA_NAME([schema_id]) = 'reporting'
                    AND [name] IN ('vw_DailySales', 'vw_StockPosition', 'usp_TopSellingProducts')
                """)
            .SingleAsync();
        var indexCount = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS [Value]
                FROM sys.indexes
                WHERE [name] IN
                (
                    'IX_StockMovements_OccurredAtUtc_StoreId_MovementType',
                    'IX_Sales_Status_FinalizedAtUtc_StoreId',
                    'IX_PurchaseOrders_CreatedAtUtc_StoreId_Status'
                )
                """)
            .SingleAsync();

        Assert.Equal(3, assetCount);
        Assert.Equal(3, indexCount);
    }

    private static async Task<JsonElement> GetOrOpenSessionAsync(HttpClient client, int cashRegisterId)
    {
        var current = await client.GetAsync($"/api/cash/sessions/current?cashRegisterId={cashRegisterId}");
        if (current.StatusCode == HttpStatusCode.OK)
        {
            var currentPayload = await current.Content.ReadFromJsonAsync<JsonDocument>();
            return currentPayload!.RootElement.Clone();
        }

        Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
        var opened = await SendWithIdempotencyAsync(
            client,
            "/api/cash/sessions",
            Guid.NewGuid(),
            new { cashRegisterId, openingAmount = 100m });
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        var payload = await opened.Content.ReadFromJsonAsync<JsonDocument>();
        return payload!.RootElement.GetProperty("session").Clone();
    }

    private static async Task<JsonElement> CreateSaleAsync(
        HttpClient client,
        int storeId,
        long cashSessionId,
        ProductContext product,
        decimal quantity)
    {
        var total = decimal.Round(product.SalePrice * quantity, 2, MidpointRounding.AwayFromZero);
        var response = await SendWithIdempotencyAsync(
            client,
            "/api/sales",
            Guid.NewGuid(),
            new
            {
                storeId,
                cashSessionId,
                discountAmount = 0m,
                items = new[] { new { productId = product.Id, quantity } },
                payments = new[] { new { method = "Cash", amount = total, reference = (string?)null } }
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return payload!.RootElement.GetProperty("sale").Clone();
    }

    private static async Task<SalesContext> GetSalesContextAsync(
        HttpClient client,
        string storeCode,
        int productCount)
    {
        var stores = await client.GetFromJsonAsync<JsonDocument>("/api/stores");
        var storeId = stores!.RootElement.EnumerateArray()
            .Single(store => store.GetProperty("code").GetString() == storeCode)
            .GetProperty("id").GetInt32();
        var registers = await client.GetFromJsonAsync<JsonDocument>($"/api/cash/registers?storeId={storeId}");
        var products = await client.GetFromJsonAsync<JsonDocument>(
            $"/api/products?page=1&pageSize={productCount}&isActive=true&sortBy=code&sortDirection=asc");
        return new SalesContext(
            storeId,
            registers!.RootElement[0].GetProperty("id").GetInt32(),
            products!.RootElement.GetProperty("items").EnumerateArray()
                .Select(item => new ProductContext(
                    item.GetProperty("id").GetInt32(),
                    item.GetProperty("salePrice").GetDecimal()))
                .ToArray());
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

    private sealed record SalesContext(int StoreId, int CashRegisterId, ProductContext[] Products);

    private sealed record ProductContext(int Id, decimal SalePrice);
}
