using System.Net.Http.Json;
using Master.Models;

namespace Master.Services;

public class TaskManagementService(
    TaskQueue taskQueue,
    WorkerRegistry workerRegistry,
    IScheduler scheduler,
    IHttpClientFactory httpClientFactory) : ITaskManagementService
{
    public TaskModel SubmitTask(string name, string description, int durationMs, TaskPriority priority)
    {
        var task = new TaskModel
        {
            Name = name,
            Description = description,
            DurationMs = durationMs,
            Priority = priority
        };

        taskQueue.Enqueue(task);
        Console.WriteLine($"Task '{name}' submitted (Id: {task.Id}, Priority: {priority}, Duration: {durationMs}ms)");
        return task;
    }

    public async Task<TaskModel?> DispatchNextTaskAsync()
    {
        var pendingTask = taskQueue.GetPending().FirstOrDefault();
        if (pendingTask == null) return null;

        var availableWorkers = workerRegistry.GetAvailableWorkers();
        var selectedWorker = scheduler.SelectWorker(availableWorkers);
        if (selectedWorker == null)
        {
            Console.WriteLine("No available workers for dispatch");
            return null;
        }

        pendingTask.Status = Models.TaskStatus.Running;
        pendingTask.AssignedWorkerId = selectedWorker.WorkerId;
        pendingTask.StartedAt = DateTime.UtcNow;

        Console.WriteLine($"Dispatching task '{pendingTask.Name}' to Worker-{selectedWorker.WorkerId}");

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var payload = new
            {
                taskId = pendingTask.Id,
                name = pendingTask.Name,
                durationMs = pendingTask.DurationMs
            };

            var response = await client.PostAsJsonAsync($"{selectedWorker.Url}/api/worker/execute", payload);

            if (!response.IsSuccessStatusCode)
            {
                pendingTask.Status = Models.TaskStatus.Pending;
                pendingTask.AssignedWorkerId = null;
                pendingTask.StartedAt = null;
                Console.WriteLine($"Failed to dispatch task to Worker-{selectedWorker.WorkerId}: HTTP {response.StatusCode}");
                return null;
            }

            Console.WriteLine($"Task '{pendingTask.Name}' dispatched to Worker-{selectedWorker.WorkerId}");
            return pendingTask;
        }
        catch (Exception ex)
        {
            pendingTask.Status = Models.TaskStatus.Pending;
            pendingTask.AssignedWorkerId = null;
            pendingTask.StartedAt = null;
            Console.WriteLine($"Dispatch error: {ex.Message}");
            return null;
        }
    }

    public void ReportTaskCompleted(Guid taskId)
    {
        var task = taskQueue.Get(taskId);
        if (task == null) return;

        task.Status = Models.TaskStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        Console.WriteLine($"Task '{task.Name}' completed by Worker-{task.AssignedWorkerId} (Duration: {(task.CompletedAt - task.StartedAt)?.TotalMilliseconds:F0}ms)");
    }

    public void ReportTaskFailed(Guid taskId, string error)
    {
        var task = taskQueue.Get(taskId);
        if (task == null) return;

        task.Status = Models.TaskStatus.Failed;
        task.CompletedAt = DateTime.UtcNow;
        task.ErrorMessage = error;
        Console.WriteLine($"Task '{task.Name}' failed on Worker-{task.AssignedWorkerId}: {error}");
    }

    public TaskModel? GetTask(Guid id) => taskQueue.Get(id);

    public IEnumerable<TaskModel> GetAllTasks() => taskQueue.GetAll();

    public object GetStats() => new
    {
        pending = taskQueue.PendingCount,
        running = taskQueue.RunningCount,
        completed = taskQueue.CompletedCount,
        failed = taskQueue.FailedCount,
        total = taskQueue.PendingCount + taskQueue.RunningCount + taskQueue.CompletedCount + taskQueue.FailedCount
    };
}
