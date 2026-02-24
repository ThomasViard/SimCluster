using System.Collections.Concurrent;
using Master.Models;

namespace Master.Services;

public class TaskQueue
{
    private readonly ConcurrentDictionary<Guid, TaskModel> _tasks = new();

    public TaskModel Enqueue(TaskModel task)
    {
        _tasks[task.Id] = task;
        return task;
    }

    public IEnumerable<TaskModel> GetPending()
    {
        return _tasks.Values
            .Where(t => t.Status == Models.TaskStatus.Pending)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt);
    }

    public TaskModel? Get(Guid id)
    {
        _tasks.TryGetValue(id, out var task);
        return task;
    }

    public IEnumerable<TaskModel> GetAll() => _tasks.Values.OrderByDescending(t => t.CreatedAt);

    public int PendingCount => _tasks.Values.Count(t => t.Status == Models.TaskStatus.Pending);
    public int RunningCount => _tasks.Values.Count(t => t.Status == Models.TaskStatus.Running);
    public int CompletedCount => _tasks.Values.Count(t => t.Status == Models.TaskStatus.Completed);
    public int FailedCount => _tasks.Values.Count(t => t.Status == Models.TaskStatus.Failed);
}
