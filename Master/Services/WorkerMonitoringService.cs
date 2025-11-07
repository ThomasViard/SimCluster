namespace Master.Services;

public class WorkerMonitoringService : IHostedService, IDisposable
{
    private readonly WorkerRegistry _registry;
    private Timer? _timer;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(15);

    public WorkerMonitoringService(WorkerRegistry registry)
    {
        _registry = registry;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Worker monitoring service started");

        _timer = new Timer(
            callback: _ => CheckWorkers(),
            state: null,
            dueTime: _checkInterval,
            period: _checkInterval
        );

        return Task.CompletedTask;
    }

    private void CheckWorkers()
    {
        _registry.CheckForDisconnectedWorkers();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Worker monitoring service stopped");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
