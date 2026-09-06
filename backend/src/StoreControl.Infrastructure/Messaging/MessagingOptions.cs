namespace StoreControl.Infrastructure.Messaging;

internal sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public bool Enabled { get; init; }
    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string VirtualHost { get; init; } = "/";
    public string TopologyPrefix { get; init; } = "storecontrol";
    public int ConnectionTimeoutMilliseconds { get; init; } = 2_000;
    public int RecoveryIntervalSeconds { get; init; } = 5;
    public int OutboxPollingMilliseconds { get; init; } = 1_000;
    public int OutboxBatchSize { get; init; } = 20;
    public int OutboxLeaseSeconds { get; init; } = 30;
    public ushort ConsumerPrefetch { get; init; } = 8;
    public int[] RetryDelaysMilliseconds { get; init; } = [5_000, 30_000, 120_000];

    public bool IsValid()
    {
        if (!Enabled)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(HostName)
            && Port is >= 1 and <= 65_535
            && !string.IsNullOrWhiteSpace(UserName)
            && !string.IsNullOrWhiteSpace(Password)
            && !string.IsNullOrWhiteSpace(VirtualHost)
            && IsSafeTopologyPrefix(TopologyPrefix)
            && ConnectionTimeoutMilliseconds is >= 250 and <= 30_000
            && RecoveryIntervalSeconds is >= 1 and <= 60
            && OutboxPollingMilliseconds is >= 100 and <= 60_000
            && OutboxBatchSize is >= 1 and <= 200
            && OutboxLeaseSeconds is >= 5 and <= 300
            && ConsumerPrefetch is >= 1 and <= 256
            && RetryDelaysMilliseconds is { Length: >= 1 and <= 5 }
            && RetryDelaysMilliseconds.All(delay => delay is >= 100 and <= 3_600_000)
            && RetryDelaysMilliseconds.SequenceEqual(RetryDelaysMilliseconds.Order());
    }

    private static bool IsSafeTopologyPrefix(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 80
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
}
