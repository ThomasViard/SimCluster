namespace Worker.Models;

public class TaskExecutionRequest
{
    public Guid TaskId { get; set; }
    public string Name { get; set; } = "";
    public int DurationMs { get; set; } = 5000;
}
