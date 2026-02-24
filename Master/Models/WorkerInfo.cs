namespace Master.Models;

public class WorkerInfo
{
    public string WorkerId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public bool IsAvailable { get; set; } = true;
    public int FreeThreads { get; set; }
    public bool IsReady { get; set; }
    public bool IsEnabled { get; set; } = true;
}
