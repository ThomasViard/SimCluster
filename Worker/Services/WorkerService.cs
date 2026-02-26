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
        if (!_state.TryStartTask())
        {
            Console.WriteLine($"Task '{request.Name}' rejected: no free threads");
            Task.Run(async () =>
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    await client.PostAsJsonAsync(
                        $"{_config.MasterUrl}/api/task/{request.TaskId}/failed",
                        new { error = "Worker busy, no free threads" });
                }
                catch { }
            });
            return;
        }

        Console.WriteLine($"Executing task '{request.Name}' ({request.DurationMs}ms)");

        _ = Task.Run(async () =>
        {
            try
            {
                SimulateCpuWork(request.DurationMs);
                _state.IncrementTasksExecuted();
                Console.WriteLine($"Task '{request.Name}' completed");

                var client = _httpClientFactory.CreateClient();
                await client.PostAsync($"{_config.MasterUrl}/api/task/{request.TaskId}/completed", null);
            }
            catch (Exception ex)
            {
                _state.IncrementTasksFailed();
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

    /// <summary>
    /// Simulates CPU-intensive work for the specified duration.
    /// Uses a tight computation loop instead of Task.Delay so that actual CPU
    /// is consumed, enabling Kubernetes HPA to detect load and autoscale.
    /// </summary>
    private static void SimulateCpuWork(int durationMs)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        double result = 0;
        while (stopwatch.ElapsedMilliseconds < durationMs)
        {
            for (int i = 0; i < 1000; i++)
            {
                result += Math.Sqrt(i * 3.14159265) * Math.Sin(i * 0.01);
            }
        }
        // Prevent compiler optimization
        if (result == double.MinValue) Console.Write("");
    }

    public object GetStatus()
    {
        return new
        {
            workerId = _config.WorkerId,
            isReady = _state.IsReady,
            freeThreads = _state.FreeThreads,
            maxThreads = _state.MaxThreads,
            busyThreads = _state.BusyThreads,
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
            maxThreads = _state.MaxThreads,
            busyThreads = _state.BusyThreads,

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
}
