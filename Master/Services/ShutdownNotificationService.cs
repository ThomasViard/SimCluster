namespace Master.Services;

public class ShutdownNotificationService(WorkerRegistry registry, IHttpClientFactory httpClientFactory)
{
    private readonly WorkerRegistry _registry = registry;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task NotifyAllWorkersAsync()
    {
        var workers = _registry.GetAllWorkers().ToList();

        if (workers.Count == 0)
        {
            Console.WriteLine("No workers to notify");
            return;
        }

        Console.WriteLine($"Notifying {workers.Count} worker(s) of shutdown...");

        var tasks = workers.Select(worker => NotifyWorkerAsync(worker.WorkerId, worker.Url));
        await Task.WhenAll(tasks);
    }

    private async Task NotifyWorkerAsync(string workerId, string workerUrl)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.PostAsync(
                $"{workerUrl}/api/worker/notifications/master-shutdown",
                null
            );

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Worker-{workerId} notified of shutdown");
            }
            else
            {
                Console.WriteLine($"Failed to notify Worker-{workerId} (HTTP {response.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to notify Worker-{workerId}: {ex.Message}");
        }
    }
}
