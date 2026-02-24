namespace Master.Models;

public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2
}

public class TaskModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public int DurationMs { get; set; } = 5000;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public int StatusValue = (int)Models.TaskStatus.Pending;
    public Models.TaskStatus Status
    {
        get => (Models.TaskStatus)Volatile.Read(ref StatusValue);
        set => Volatile.Write(ref StatusValue, (int)value);
    }
    public string? AssignedWorkerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed
}
