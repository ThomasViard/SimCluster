using Master.Models;
using Master.Services;
using Microsoft.AspNetCore.Mvc;

namespace Master.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaskController(ITaskManagementService taskService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] TaskSubmitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required" });

        var task = taskService.SubmitTask(
            request.Name,
            request.Description ?? "",
            request.DurationMs > 0 ? request.DurationMs : 5000,
            request.Priority
        );

        var dispatched = await taskService.DispatchNextTaskAsync();

        return Ok(new
        {
            task,
            dispatched = dispatched != null
        });
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(new
        {
            stats = taskService.GetStats(),
            tasks = taskService.GetAllTasks()
        });
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        var task = taskService.GetTask(id);
        if (task == null) return NotFound(new { error = "Task not found" });
        return Ok(task);
    }

    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        return Ok(taskService.GetStats());
    }

    [HttpPost("{id:guid}/completed")]
    public IActionResult ReportCompleted(Guid id)
    {
        taskService.ReportTaskCompleted(id);
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/failed")]
    public IActionResult ReportFailed(Guid id, [FromBody] TaskFailedRequest request)
    {
        taskService.ReportTaskFailed(id, request.Error ?? "Unknown error");
        return Ok(new { success = true });
    }
}

public class TaskSubmitRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int DurationMs { get; set; } = 5000;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
}

public class TaskFailedRequest
{
    public string? Error { get; set; }
}
