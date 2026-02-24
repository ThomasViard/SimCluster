using System.Net.Http.Json;

namespace Dashboard.Services;

public class ClusterApiService(HttpClient httpClient)
{
    public async Task<WorkersResponse?> GetWorkersAsync()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<WorkersResponse>("/api/master/workers");
        }
        catch { return null; }
    }

    public async Task<TasksResponse?> GetTasksAsync()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<TasksResponse>("/api/task");
        }
        catch { return null; }
    }

    public async Task<bool> SubmitTaskAsync(TaskSubmitRequest request)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("/api/task", request);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

public class WorkersResponse
{
    public int TotalWorkers { get; set; }
    public List<WorkerDto> Workers { get; set; } = [];
}

public class WorkerDto
{
    public string WorkerId { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime RegisteredAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public bool IsAvailable { get; set; }
    public int FreeThreads { get; set; }
    public bool IsReady { get; set; }
}

public class TasksResponse
{
    public TaskStatsDto Stats { get; set; } = new();
    public List<TaskDto> Tasks { get; set; } = [];
}

public class TaskStatsDto
{
    public int Pending { get; set; }
    public int Running { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Total { get; set; }
}

public class TaskDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int DurationMs { get; set; }
    public int Priority { get; set; }
    public int Status { get; set; }
    public string? AssignedWorkerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public string StatusLabel => Status switch
    {
        0 => "Pending",
        1 => "Running",
        2 => "Completed",
        3 => "Failed",
        _ => "Unknown"
    };

    public string StatusCss => Status switch
    {
        0 => "badge-pending",
        1 => "badge-running",
        2 => "badge-completed",
        3 => "badge-failed",
        _ => ""
    };

    public string PriorityLabel => Priority switch
    {
        0 => "Low",
        1 => "Normal",
        2 => "High",
        _ => "?"
    };
}

public class TaskSubmitRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int DurationMs { get; set; } = 5000;
    public int Priority { get; set; } = 1;
}
