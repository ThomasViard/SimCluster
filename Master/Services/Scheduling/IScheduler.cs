using Master.Models;

namespace Master.Services;

public interface IScheduler
{
    WorkerInfo? SelectWorker(IEnumerable<WorkerInfo> availableWorkers);
}
