using Common.Models;
using Master.Services;
using Microsoft.AspNetCore.Mvc;

namespace Master.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MasterController(IWorkerManagementService workerManagementService) : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok(new { message = "Pong", timestamp = DateTime.UtcNow });
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] WorkerRegistrationRequest request)
    {
        if (string.IsNullOrEmpty(request.WorkerId) || string.IsNullOrEmpty(request.Url))
        {
            return BadRequest(new { error = "WorkerId and Url are required" });
        }

        workerManagementService.RegisterWorker(request.WorkerId, request.Url);

        return Ok(new
        {
            success = true,
            message = "Worker registered successfully",
            workerId = request.WorkerId
        });
    }

    [HttpPost("heartbeat")]
    public IActionResult Heartbeat([FromBody] WorkerHeartbeatRequest request)
    {
        if (string.IsNullOrEmpty(request.WorkerId))
        {
            return BadRequest(new { error = "WorkerId is required" });
        }
        var existing = workerManagementService.GetWorker(request.WorkerId);
        if (existing == null)
        {
            // Unknown worker: force caller to re-register
            return NotFound(new { error = $"Worker {request.WorkerId} not registered" });
        }

        workerManagementService.UpdateHeartbeat(request.WorkerId, request.IsReady, request.FreeThreads);
        return Ok(new { success = true, message = "Heartbeat received" });
    }

    [HttpGet("workers")]
    public IActionResult GetAll()
    {
        var workers = workerManagementService.GetAllWorkers();

        return Ok(new
        {
            totalWorkers = workerManagementService.GetWorkerCount(),
            workers = workers
        });
    }

    [HttpGet("workers/available")]
    public IActionResult GetAvailable()
    {
        var workers = workerManagementService.GetAvailableWorkers();

        return Ok(new
        {
            availableWorkers = workerManagementService.GetAvailableWorkerCount(),
            workers = workers
        });
    }

    [HttpGet("workers/{workerId}")]
    public IActionResult GetById(string workerId)
    {
        var worker = workerManagementService.GetWorker(workerId);

        if (worker == null)
        {
            return NotFound(new { error = $"Worker {workerId} not found" });
        }

        return Ok(worker);
    }

    [HttpPost("workers/{workerId}/enable")]
    public IActionResult EnableWorker(string workerId)
    {
        var worker = workerManagementService.GetWorker(workerId);
        if (worker == null)
            return NotFound(new { error = $"Worker {workerId} not found" });
        workerManagementService.EnableWorker(workerId);
        return Ok(new { success = true, message = $"Worker {workerId} enabled" });
    }

    [HttpPost("workers/{workerId}/disable")]
    public IActionResult DisableWorker(string workerId)
    {
        var worker = workerManagementService.GetWorker(workerId);
        if (worker == null)
            return NotFound(new { error = $"Worker {workerId} not found" });
        workerManagementService.DisableWorker(workerId);
        return Ok(new { success = true, message = $"Worker {workerId} disabled" });
    }

    [HttpDelete("workers/{workerId}")]
    public IActionResult RemoveWorker(string workerId)
    {
        var worker = workerManagementService.GetWorker(workerId);
        if (worker == null)
            return NotFound(new { error = $"Worker {workerId} not found" });
        workerManagementService.RemoveWorker(workerId);
        return Ok(new { success = true, message = $"Worker {workerId} removed" });
    }

    [HttpPost("notifications/worker-disconnect")]
    public IActionResult WorkerDisconnect([FromBody] WorkerDisconnectRequest request)
    {
        if (string.IsNullOrEmpty(request.WorkerId))
        {
            return BadRequest(new { error = "WorkerId is required" });
        }

        workerManagementService.HandleWorkerDisconnect(request.WorkerId, request.Reason);

        return Ok(new { success = true, message = "Worker disconnect acknowledged" });
    }
}
