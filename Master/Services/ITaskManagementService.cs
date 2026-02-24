using Master.Models;

namespace Master.Services;

public interface ITaskManagementService
{
    TaskModel SubmitTask(string name, string description, int durationMs, TaskPriority priority);
    Task<TaskModel?> DispatchNextTaskAsync();
    void ReportTaskCompleted(Guid taskId);
    void ReportTaskFailed(Guid taskId, string error);
    TaskModel? GetTask(Guid id);
    IEnumerable<TaskModel> GetAllTasks();
    object GetStats();
}
