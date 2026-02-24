namespace Master.Services;

public class TaskDispatcherService(
    IServiceScopeFactory scopeFactory,
    TaskQueue taskQueue,
    WorkerRegistry workerRegistry) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("Task dispatcher started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (taskQueue.PendingCount > 0 && workerRegistry.GetAvailableWorkers().Any())
                {
                    using var scope = scopeFactory.CreateScope();
                    var taskService = scope.ServiceProvider.GetRequiredService<ITaskManagementService>();

                    var dispatched = 0;
                    while (taskQueue.PendingCount > 0)
                    {
                        var result = await taskService.DispatchNextTaskAsync();
                        if (result == null) break;
                        dispatched++;
                    }

                    if (dispatched > 0)
                        Console.WriteLine($"Dispatcher: {dispatched} tasks dispatched ({taskQueue.PendingCount} still pending)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dispatcher error: {ex.Message}");
            }

            await Task.Delay(500, stoppingToken);
        }
    }
}
