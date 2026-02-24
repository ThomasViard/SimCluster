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

    public TaskModel? TryClaimForDispatch()
    {
        var pending = GetPending().ToList();
        foreach (var task in pending)
        {
            var original = (int)task.Status;
            if (original == (int)Models.TaskStatus.Pending &&
                Interlocked.CompareExchange(ref task.StatusValue, (int)Models.TaskStatus.Running, original) == original)
            {
                return task;
            }
        }
        return null;
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
