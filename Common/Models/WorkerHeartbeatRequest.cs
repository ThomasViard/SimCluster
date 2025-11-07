namespace Common.Models;

public class WorkerHeartbeatRequest
{
    public string WorkerId { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public int FreeThreads { get; set; }
    public DateTime Timestamp { get; set; }
}
