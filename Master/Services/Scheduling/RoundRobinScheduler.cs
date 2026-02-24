using Master.Models;

namespace Master.Services;

public class RoundRobinScheduler : IScheduler
{
    private int _currentIndex;

    public WorkerInfo? SelectWorker(IEnumerable<WorkerInfo> availableWorkers)
    {
        var workers = availableWorkers.ToList();
        if (workers.Count == 0) return null;

        var index = Interlocked.Increment(ref _currentIndex) % workers.Count;
        return workers[index];
    }
}
