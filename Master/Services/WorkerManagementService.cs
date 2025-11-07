using Master.Models;

namespace Master.Services;

public class WorkerManagementService(WorkerRegistry registry) : IWorkerManagementService
{
    public void RegisterWorker(string workerId, string url)
    {
        registry.RegisterWorker(workerId, url);
        Console.WriteLine($"Worker {workerId} registered successfully");
    }

    public void UpdateHeartbeat(string workerId, bool isReady, int freeThreads)
    {
        registry.UpdateHeartbeat(workerId, isReady, freeThreads);
        Console.WriteLine($"Heartbeat from Worker-{workerId} (Ready: {isReady}, Free: {freeThreads})");
    }

    public void HandleWorkerDisconnect(string workerId, string reason)
    {
        registry.MarkWorkerDisconnected(workerId);
        Console.WriteLine($"Worker-{workerId} disconnected gracefully (Reason: {reason})");
    }

    public IEnumerable<WorkerInfo> GetAllWorkers()
    {
        return registry.GetAllWorkers();
    }

    public IEnumerable<WorkerInfo> GetAvailableWorkers()
    {
        return registry.GetAvailableWorkers();
    }

    public WorkerInfo? GetWorker(string workerId)
    {
        return registry.GetWorker(workerId);
    }

    public void RemoveWorker(string workerId)
    {
        registry.RemoveWorker(workerId);
    }

    public int GetWorkerCount()
    {
        return registry.GetWorkerCount();
    }

    public int GetAvailableWorkerCount()
    {
        return registry.GetAvailableWorkerCount();
    }
}
