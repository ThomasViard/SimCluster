using Master.Models;

namespace Master.Services;

public interface IWorkerManagementService
{
    void RegisterWorker(string workerId, string url);
    void UpdateHeartbeat(string workerId, bool isReady, int freeThreads);
    void HandleWorkerDisconnect(string workerId, string reason);
    IEnumerable<WorkerInfo> GetAllWorkers();
    IEnumerable<WorkerInfo> GetAvailableWorkers();
    WorkerInfo? GetWorker(string workerId);
    void RemoveWorker(string workerId);
    int GetWorkerCount();
    int GetAvailableWorkerCount();
}
