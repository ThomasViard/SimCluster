using Master.Models;

namespace Master.Services;

public class LeastLoadedScheduler : IScheduler
{
    public WorkerInfo? SelectWorker(IEnumerable<WorkerInfo> availableWorkers)
    {
        return availableWorkers
            .OrderByDescending(w => w.FreeThreads)
            .ThenBy(w => w.LastHeartbeat)
            .FirstOrDefault();
    }
}
