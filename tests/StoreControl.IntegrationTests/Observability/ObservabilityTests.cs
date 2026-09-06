using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StoreControl.IntegrationTests.Support;

namespace StoreControl.IntegrationTests.Observability;

public sealed class ObservabilityTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task Http_request_exposes_distinct_correlation_and_trace_ids_and_contains_sql_span()
    {
        var stoppedActivities = new ConcurrentBag<(string Source, string TraceId)>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.Contains("SqlClient", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stoppedActivities.Add((
                activity.Source.Name,
                activity.TraceId.ToString()))
        };
        ActivitySource.AddActivityListener(listener);

        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = "unknown-observability@storecontrol.local",
                password = "Unknown_password_2026!"
            })
        };
        request.Headers.Add("X-Correlation-ID", "observability-integration");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "observability-integration",
            Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        var traceId = Assert.Single(response.Headers.GetValues("X-Trace-ID"));
        Assert.Equal(32, traceId.Length);

        var problem = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("observability-integration", problem!.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal(traceId, problem.RootElement.GetProperty("traceId").GetString());

        var health = await client.GetAsync("/health/ready");
        health.EnsureSuccessStatusCode();
        Assert.Contains(stoppedActivities, activity =>
            activity.Source.Contains("SqlClient", StringComparison.Ordinal)
            && activity.TraceId.Length == 32);
    }
}
