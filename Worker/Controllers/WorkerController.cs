using Microsoft.AspNetCore.Mvc;
using Worker.Services;

namespace Worker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkerController(IWorkerService workerService) : ControllerBase
{
    private readonly IWorkerService _workerService = workerService;

    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok(new { message = "Pong", timestamp = DateTime.UtcNow });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var status = _workerService.GetStatus();
        return Ok(status);
    }

    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        var metrics = _workerService.GetMetrics();
        return Ok(metrics);
    }

    [HttpPost("notifications/master-shutdown")]
    public IActionResult HandleMasterShutdown()
    {
        _workerService.HandleMasterShutdown();
        return Ok(new { message = "Master shutdown acknowledged" });
    }
}
