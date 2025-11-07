using Microsoft.Extensions.Options;
using Worker.Models;

namespace Worker.Services;

public class WorkerService : IWorkerService
{
    private readonly WorkerConfiguration _config;
    private readonly WorkerState _state;
    private bool _masterIsShutdown = false;

    public WorkerService(IOptions<WorkerConfiguration> config, WorkerState state)
    {
        _config = config.Value;
        _state = state;
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
