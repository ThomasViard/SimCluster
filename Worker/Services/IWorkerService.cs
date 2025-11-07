namespace Worker.Services;

public interface IWorkerService
{
    object GetStatus();
    object GetMetrics();
    void HandleMasterShutdown();
}
