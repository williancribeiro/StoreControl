using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using StoreControl.Application.Messaging;
using StoreControl.Infrastructure.Messaging;
using StoreControl.Infrastructure.Persistence;
using StoreControl.IntegrationTests.Support;

namespace StoreControl.IntegrationTests.Messaging;

public sealed class RabbitMqFlowTests(RabbitMqApiFactory factory) : IClassFixture<RabbitMqApiFactory>
{
    [Fact]
    public async Task Sale_flows_through_confirmed_outbox_consumer_retries_idempotency_and_dlq()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var saleId = await CreateSaleAsync(client);

        var outbox = await WaitForPublishedAndProcessedAsync(saleId);
        Assert.NotNull(outbox.PublishedAtUtc);
        Assert.True(outbox.PublishAttempts >= 1);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<RabbitMqPublisher>();
            await publisher.PublishOutboxAsync(outbox, default);
        }

        await Task.Delay(500);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
            Assert.Equal(1, await dbContext.InboxMessages.CountAsync(item => item.MessageId == outbox.Id));
            Assert.Equal(1, await dbContext.SaleReceiptDeliveries.CountAsync(item => item.MessageId == outbox.Id));
        }

        var poisonId = Guid.NewGuid();
        var original = MessagingContracts.DeserializeSaleFinalized(Encoding.UTF8.GetBytes(outbox.Payload));
        var poison = original with { EventId = poisonId };
        await PublishAsync(
            poisonId,
            JsonSerializer.SerializeToUtf8Bytes(poison, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var deadLetter = await WaitForDeadLetterAsync(poisonId);
        Assert.Equal(3, RabbitMessageHeaders.RetryCount(deadLetter.BasicProperties));

        var malformedId = Guid.NewGuid();
        await PublishAsync(malformedId, "not-json"u8.ToArray());
        var malformedDeadLetter = await WaitForDeadLetterAsync(malformedId);
        Assert.Equal(0, RabbitMessageHeaders.RetryCount(malformedDeadLetter.BasicProperties));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
            Assert.Equal(0, await dbContext.InboxMessages.CountAsync(item => item.MessageId == poisonId));
            Assert.Equal(1, await dbContext.SaleReceiptDeliveries.CountAsync(item => item.SaleId == saleId));
        }
    }

    private async Task<long> CreateSaleAsync(HttpClient client)
    {
        var stores = await client.GetFromJsonAsync<JsonDocument>("/api/stores");
        var storeId = stores!.RootElement.EnumerateArray().First().GetProperty("id").GetInt32();
        var registers = await client.GetFromJsonAsync<JsonDocument>($"/api/cash/registers?storeId={storeId}");
        var registerId = registers!.RootElement[0].GetProperty("id").GetInt32();
        var current = await client.GetAsync($"/api/cash/sessions/current?cashRegisterId={registerId}");
        JsonElement session;
        if (current.StatusCode == HttpStatusCode.NoContent)
        {
            var opened = await SendWithIdempotencyAsync(
                client,
                "/api/cash/sessions",
                Guid.NewGuid(),
                new { cashRegisterId = registerId, openingAmount = 100m });
            opened.EnsureSuccessStatusCode();
            session = (await opened.Content.ReadFromJsonAsync<JsonDocument>())!
                .RootElement.GetProperty("session").Clone();
        }
        else
        {
            current.EnsureSuccessStatusCode();
            session = (await current.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.Clone();
        }

        var products = await client.GetFromJsonAsync<JsonDocument>(
            "/api/products?page=1&pageSize=1&isActive=true&sortBy=code&sortDirection=asc");
        var product = products!.RootElement.GetProperty("items")[0];
        var response = await SendWithIdempotencyAsync(
            client,
            "/api/sales",
            Guid.NewGuid(),
            new
            {
                storeId,
                cashSessionId = session.GetProperty("id").GetInt64(),
                discountAmount = 0m,
                items = new[] { new { productId = product.GetProperty("id").GetInt32(), quantity = 1m } },
                payments = new[]
                {
                    new
                    {
                        method = "Cash",
                        amount = product.GetProperty("salePrice").GetDecimal(),
                        reference = (string?)null
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return payload!.RootElement.GetProperty("sale").GetProperty("id").GetInt64();
    }

    private async Task<OutboxMessage> WaitForPublishedAndProcessedAsync(long saleId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
            var outbox = await dbContext.OutboxMessages
                .AsNoTracking()
                .SingleOrDefaultAsync(item => dbContext.Sales.Any(sale => sale.Id == saleId && sale.OperationId == item.Id));
            if (outbox?.PublishedAtUtc is not null
                && await dbContext.InboxMessages.AnyAsync(item => item.MessageId == outbox.Id)
                && await dbContext.SaleReceiptDeliveries.AnyAsync(item => item.MessageId == outbox.Id))
            {
                return outbox;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("The sale event was not published and processed within 20 seconds.");
    }

    private async Task PublishAsync(Guid messageId, ReadOnlyMemory<byte> body)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<RabbitMqPublisher>();
        var topology = scope.ServiceProvider.GetRequiredService<RabbitTopology>();
        var properties = RabbitMqPublisher.CreateProperties(
            messageId,
            MessagingContracts.SaleFinalizedType,
            Guid.NewGuid().ToString("D"),
            DateTime.UtcNow,
            retryCount: 0,
            activity: null);
        await publisher.PublishAsync(
            topology.EventsExchange,
            MessagingContracts.SaleFinalizedRoutingKey,
            properties,
            body,
            default);
    }

    private async Task<BasicGetResult> WaitForDeadLetterAsync(Guid messageId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var connection = scope.ServiceProvider.GetRequiredService<RabbitMqConnection>();
        var topology = scope.ServiceProvider.GetRequiredService<RabbitTopology>();
        var rabbitConnection = await connection.GetAsync(default);
        await using var channel = await rabbitConnection.CreateChannelAsync();
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var result = await channel.BasicGetAsync(topology.SaleFinalizedDeadLetterQueue, autoAck: true);
            if (result is not null && result.BasicProperties.MessageId == messageId.ToString("D"))
            {
                return result;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("The exhausted message did not reach the dead-letter queue within 20 seconds.");
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

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "admin@storecontrol.local", password = RabbitMqApiFactory.AdminPassword });
        login.EnsureSuccessStatusCode();
        var payload = await login.Content.ReadFromJsonAsync<JsonDocument>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            payload!.RootElement.GetProperty("accessToken").GetString());
        return client;
    }
}
