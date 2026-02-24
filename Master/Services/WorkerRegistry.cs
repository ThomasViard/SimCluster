using System.Collections.Concurrent;
using Master.Models;

namespace Master.Services;

public class WorkerRegistry
{
    private readonly ConcurrentDictionary<string, WorkerInfo> _workers = new();
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(30);

    public void RegisterWorker(string workerId, string url)
    {
        var isNewWorker = !_workers.ContainsKey(workerId);

        var worker = new WorkerInfo
        {
            WorkerId = workerId,
            Url = url,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow,
            IsAvailable = true,
            IsEnabled = true
        };

        _workers.AddOrUpdate(workerId, worker, (key, existing) =>
        {
            existing.Url = url;
            existing.LastHeartbeat = DateTime.UtcNow;
            existing.IsAvailable = true;
            return existing;
        });

        if (isNewWorker)
        {
            Console.WriteLine($"Worker-{workerId} connected from {url}");
        }
        else
        {
            Console.WriteLine($"Worker-{workerId} re-registered");
        }
    }

    public void UpdateHeartbeat(string workerId, bool isReady, int freeThreads)
    {
        if (_workers.TryGetValue(workerId, out var worker))
        {
            var wasOffline = !worker.IsAvailable;

            worker.LastHeartbeat = DateTime.UtcNow;
            worker.IsReady = isReady;
            worker.FreeThreads = freeThreads;
            worker.IsAvailable = true;

            if (wasOffline)
            {
                Console.WriteLine($"Worker-{workerId} reconnected (was offline)");
            }
        }
    }

    public void CheckForDisconnectedWorkers()
    {
        var now = DateTime.UtcNow;

        foreach (var worker in _workers.Values)
        {
            var timeSinceLastHeartbeat = now - worker.LastHeartbeat;

            if (worker.IsAvailable && timeSinceLastHeartbeat > _heartbeatTimeout)
            {
                worker.IsAvailable = false;
                Console.WriteLine($"Worker-{worker.WorkerId} disconnected (timeout after {timeSinceLastHeartbeat.TotalSeconds:F0}s)");
            }
        }
    }

    public IEnumerable<WorkerInfo> GetAllWorkers()
    {
        return [.. _workers.Values];
    }

    public IEnumerable<WorkerInfo> GetAvailableWorkers()
    {
        var threshold = DateTime.UtcNow.AddSeconds(-30);
        return [.. _workers.Values.Where(w => w.IsEnabled && w.IsAvailable && w.LastHeartbeat > threshold && w.IsReady && w.FreeThreads > 0)];
    }

    public void ReleaseWorkerThread(string workerId)
    {
        if (_workers.TryGetValue(workerId, out var worker))
        {
            worker.FreeThreads++;
        }
    }

    public void EnableWorker(string workerId)
    {
        if (_workers.TryGetValue(workerId, out var worker))
        {
            worker.IsEnabled = true;
            Console.WriteLine($"Worker-{workerId} enabled");
        }
    }

    public void DisableWorker(string workerId)
    {
        if (_workers.TryGetValue(workerId, out var worker))
        {
            worker.IsEnabled = false;
            Console.WriteLine($"Worker-{workerId} disabled");
        }
    }

    public WorkerInfo? GetWorker(string workerId)
    {
        _workers.TryGetValue(workerId, out var worker);
        return worker;
    }

    public void MarkWorkerDisconnected(string workerId)
    {
        if (_workers.TryGetValue(workerId, out var worker))
        {
            worker.IsAvailable = false;
        }
    }

    public void RemoveWorker(string workerId)
    {
        if (_workers.TryRemove(workerId, out _))
        {
            Console.WriteLine($"Worker-{workerId} removed from registry");
        }
    }

    public int GetWorkerCount() => _workers.Count;

    public int GetAvailableWorkerCount() => GetAvailableWorkers().Count();
}
