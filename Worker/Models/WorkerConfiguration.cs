namespace Worker.Models;

public class WorkerConfiguration
{
    required public string WorkerId { get; set; } = string.Empty;
    required public string WorkerUrl { get; set; } = string.Empty;
    required public string MasterUrl { get; set; } = string.Empty;
}
