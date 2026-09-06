using StoreControl.Infrastructure.Messaging;

namespace StoreControl.UnitTests.Messaging;

public sealed class MessagingConfigurationTests
{
    [Fact]
    public void Disabled_messaging_does_not_require_credentials()
    {
        var options = new MessagingOptions();

        Assert.True(options.IsValid());
    }

    [Fact]
    public void Enabled_messaging_requires_safe_complete_configuration()
    {
        var valid = CreateValidOptions();
        var unsafePrefix = CreateValidOptions("storecontrol/test");
        var unorderedRetry = CreateValidOptions(retryDelays: [1_000, 500]);

        Assert.True(valid.IsValid());
        Assert.False(unsafePrefix.IsValid());
        Assert.False(unorderedRetry.IsValid());
    }

    [Fact]
    public void Topology_names_are_isolated_by_prefix()
    {
        var topology = new RabbitTopology(CreateValidOptions("storecontrol.tests.42"));

        Assert.Equal("storecontrol.tests.42.events", topology.EventsExchange);
        Assert.Equal("storecontrol.tests.42.worker.sale-finalized", topology.SaleFinalizedQueue);
        Assert.Equal("sale.finalized.v1.retry.2", topology.RetryRoutingKey(1));
        Assert.Equal("storecontrol.tests.42.worker.sale-finalized.retry.2", topology.RetryQueue(1));
        Assert.Equal("storecontrol.tests.42.worker.sale-finalized.dlq", topology.SaleFinalizedDeadLetterQueue);
    }

    private static MessagingOptions CreateValidOptions(
        string prefix = "storecontrol.tests",
        int[]? retryDelays = null) =>
        new()
        {
            Enabled = true,
            HostName = "localhost",
            Port = 5672,
            UserName = "test",
            Password = "not-a-real-secret",
            VirtualHost = "/",
            TopologyPrefix = prefix,
            RetryDelaysMilliseconds = retryDelays ?? [500, 1_000]
        };
}
