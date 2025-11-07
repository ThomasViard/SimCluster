namespace Common.Models;

public class WorkerDisconnectRequest
{
    public string WorkerId { get; set; } = string.Empty;
    public string Reason { get; set; } = "shutdown";
}
