using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using StoreControl.IntegrationTests.Support;

namespace StoreControl.IntegrationTests.MasterData;

public sealed class CatalogAndPartiesTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task Product_crud_enforces_duplicates_concurrency_and_category_integrity()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var categoryResponse = await client.PostAsJsonAsync("/api/categories", new
        {
            code = $"CAT-{suffix}",
            name = "Categoria de integração",
            description = "Criada pelo teste de integração."
        });
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        var category = await categoryResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var categoryId = category!.RootElement.GetProperty("id").GetInt32();

        var productResponse = await client.PostAsJsonAsync("/api/products", new
        {
            code = $"PRD-{suffix}",
            barcode = $"7{Random.Shared.NextInt64(100000000000, 999999999999)}",
            name = "Produto de integração",
            description = "Versão inicial",
            categoryId,
            costPrice = 10.12345m,
            salePrice = 17.505m
        });
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = await productResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var productId = product!.RootElement.GetProperty("id").GetInt32();
        var originalRowVersion = product.RootElement.GetProperty("rowVersion").GetString();
        Assert.Equal(10.1235m, product.RootElement.GetProperty("costPrice").GetDecimal());
        Assert.Equal(17.51m, product.RootElement.GetProperty("salePrice").GetDecimal());

        var duplicate = await client.PostAsJsonAsync("/api/products", new
        {
            code = $"prd-{suffix}",
            name = "Código repetido",
            categoryId,
            costPrice = 1m,
            salePrice = 2m
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var inactivated = await client.PutAsJsonAsync($"/api/products/{productId}", new
        {
            code = $"PRD-{suffix}",
            barcode = product.RootElement.GetProperty("barcode").GetString(),
            name = "Produto de integração editado",
            description = "Versão editada",
            categoryId,
            costPrice = 11m,
            salePrice = 18m,
            isActive = false,
            rowVersion = originalRowVersion
        });
        Assert.Equal(HttpStatusCode.OK, inactivated.StatusCode);
        var inactiveProduct = await inactivated.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.False(inactiveProduct!.RootElement.GetProperty("isActive").GetBoolean());

        var stale = await client.PutAsJsonAsync($"/api/products/{productId}", new
        {
            code = $"PRD-{suffix}",
            name = "Alteração obsoleta",
            categoryId,
            costPrice = 11m,
            salePrice = 18m,
            isActive = true,
            rowVersion = originalRowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var reactivated = await client.PutAsJsonAsync($"/api/products/{productId}", new
        {
            code = $"PRD-{suffix}",
            barcode = inactiveProduct.RootElement.GetProperty("barcode").GetString(),
            name = "Produto de integração editado",
            description = "Versão editada",
            categoryId,
            costPrice = 11m,
            salePrice = 18m,
            isActive = true,
            rowVersion = inactiveProduct.RootElement.GetProperty("rowVersion").GetString()
        });
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);

        var categoryInactivation = await client.PutAsJsonAsync($"/api/categories/{categoryId}", new
        {
            code = category.RootElement.GetProperty("code").GetString(),
            name = category.RootElement.GetProperty("name").GetString(),
            description = category.RootElement.GetProperty("description").GetString(),
            isActive = false,
            rowVersion = category.RootElement.GetProperty("rowVersion").GetString()
        });
        Assert.Equal(HttpStatusCode.OK, categoryInactivation.StatusCode);

        var inactiveCategoryAssignment = await client.PostAsJsonAsync("/api/products", new
        {
            code = $"INACTIVE-{suffix}",
            name = "Associação inválida",
            categoryId,
            costPrice = 1m,
            salePrice = 2m
        });
        Assert.Equal(HttpStatusCode.BadRequest, inactiveCategoryAssignment.StatusCode);
    }

    [Fact]
    public async Task Seed_supports_product_search_filter_sort_and_pagination()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "gerente@storecontrol.local",
            SqlServerApiFactory.UserPassword);

        var response = await client.GetAsync(
            "/api/products?page=2&pageSize=10&search=PRD-&isActive=true&sortBy=code&sortDirection=desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(payload!.RootElement.GetProperty("totalCount").GetInt32() >= 120);
        var items = payload.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(10, items.Length);
        var codes = items.Select(item => item.GetProperty("code").GetString()).ToArray();
        Assert.Equal(codes.OrderByDescending(code => code, StringComparer.Ordinal), codes);
    }

    [Fact]
    public async Task Party_endpoints_normalize_duplicates_and_enforce_profile_permissions()
    {
        using var seller = await CreateAuthenticatedClientAsync(
            "vendedor@storecontrol.local",
            SqlServerApiFactory.UserPassword);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var customer = await seller.PostAsJsonAsync("/api/customers", new
        {
            code = $"CLI-{suffix}",
            name = "Cliente de integração",
            document = "123.456.789-01",
            email = $"cliente-{suffix}@example.local",
            phone = "(11) 99999-0000"
        });
        Assert.Equal(HttpStatusCode.Created, customer.StatusCode);

        var duplicateDocument = await seller.PostAsJsonAsync("/api/customers", new
        {
            code = $"CL2-{suffix}",
            name = "Cliente repetido",
            document = "12345678901"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateDocument.StatusCode);

        var forbiddenSupplier = await seller.PostAsJsonAsync("/api/suppliers", new
        {
            code = $"FOR-{suffix}",
            name = "Fornecedor bloqueado"
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenSupplier.StatusCode);

        using var administrator = await CreateAuthenticatedClientAsync(
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        var supplier = await administrator.PostAsJsonAsync("/api/suppliers", new
        {
            code = $"FOR-{suffix}",
            name = "Fornecedor de integração",
            document = "12.345.678/0001-99",
            email = $"fornecedor-{suffix}@example.local"
        });
        Assert.Equal(HttpStatusCode.Created, supplier.StatusCode);
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
