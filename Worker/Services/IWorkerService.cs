using Worker.Models;

namespace Worker.Services;

public interface IWorkerService
{
    object GetStatus();
    object GetMetrics();
    void HandleMasterShutdown();
    void ExecuteTaskAsync(TaskExecutionRequest request);
}
