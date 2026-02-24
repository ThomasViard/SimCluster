using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Worker.Models;

namespace Worker.Services;

public class WorkerService : IWorkerService
{
    private readonly WorkerConfiguration _config;
    private readonly WorkerState _state;
    private readonly IHttpClientFactory _httpClientFactory;
    private bool _masterIsShutdown = false;

    public WorkerService(IOptions<WorkerConfiguration> config, WorkerState state, IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _state = state;
        _httpClientFactory = httpClientFactory;
    }

    public void ExecuteTaskAsync(TaskExecutionRequest request)
    {
        _state.StartTask();
        Console.WriteLine($"Executing task '{request.Name}' ({request.DurationMs}ms)");

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(request.DurationMs);
                Console.WriteLine($"Task '{request.Name}' completed");

                var client = _httpClientFactory.CreateClient();
                await client.PostAsync($"{_config.MasterUrl}/api/task/{request.TaskId}/completed", null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Task '{request.Name}' failed: {ex.Message}");

                try
                {
                    var client = _httpClientFactory.CreateClient();
                    await client.PostAsJsonAsync(
                        $"{_config.MasterUrl}/api/task/{request.TaskId}/failed",
                        new { error = ex.Message });
                }
                catch { }
            }
            finally
            {
                _state.FinishTask();
            }
        });
    }

    public object GetStatus()
    {
        return new
        {
            workerId = _config.WorkerId,
            isReady = _state.IsReady,
            freeThreads = _state.FreeThreads,
            maxThreads = _state.FreeThreads + GetBusyThreads(),
            masterConnection = new
            {
                masterShutdown = _masterIsShutdown
            },
            timestamp = DateTime.UtcNow
        };
    }

    public object GetMetrics()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();

        return new
        {
            workerId = _config.WorkerId,
            timestamp = DateTime.UtcNow,

            isReady = _state.IsReady,
            freeThreads = _state.FreeThreads,
            maxThreads = _state.FreeThreads + GetBusyThreads(),

            memoryUsedMB = process.WorkingSet64 / (1024.0 * 1024.0),
            cpuTimeSeconds = process.TotalProcessorTime.TotalSeconds,
            threadCount = process.Threads.Count,
            uptimeSeconds = (DateTime.Now - process.StartTime).TotalSeconds,

            masterConnection = new
            {
                masterShutdown = _masterIsShutdown
            }
        };
    }

    public void HandleMasterShutdown()
    {
        _masterIsShutdown = true;
        Console.WriteLine("Master has shut down");
        Console.WriteLine("Worker is now isolated from cluster");
        Console.WriteLine("Worker will continue running and attempt to reconnect if Master restarts");
    }

    private int GetBusyThreads()
    {
        // Pour l'instant, retourner 0. À implémenter si nécessaire
        return 0;
    }
}
