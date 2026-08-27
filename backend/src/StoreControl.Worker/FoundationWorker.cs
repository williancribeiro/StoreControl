namespace StoreControl.Worker;

public sealed class FoundationWorker(ILogger<FoundationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "StoreControl Worker started; business background services are not registered in the foundation phase");

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
