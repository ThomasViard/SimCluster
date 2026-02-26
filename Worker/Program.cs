using Common;
using Worker.Models;
using Worker.Services;

var workerId = Environment.GetEnvironmentVariable("WORKER_ID") ?? Environment.MachineName;
var masterUrl = Environment.GetEnvironmentVariable("MASTER_URL") ?? "http://localhost:8080";
var workerPort = Environment.GetEnvironmentVariable("WORKER_PORT") ?? "5001";
var workerUrl = Environment.GetEnvironmentVariable("WORKER_URL")
    ?? $"http://{System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.First(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)}:{workerPort}";

Console.SetOut(new PrefixedConsoleWriter(Console.Out, $"[Worker-{workerId}] "));

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.None);

builder.Services.AddControllers();

builder.Services.Configure<WorkerConfiguration>(options =>
{
    options.WorkerId = workerId;
    options.WorkerUrl = workerUrl;
    options.MasterUrl = masterUrl;
});
var port = int.Parse(workerPort);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});

var maxThreadsEnv = Environment.GetEnvironmentVariable("MAX_THREADS");
var maxThreads = maxThreadsEnv != null ? int.Parse(maxThreadsEnv) : (int?)null;
builder.Services.AddSingleton(new WorkerState(maxThreads));
builder.Services.AddScoped<IWorkerService, WorkerService>();
builder.Services.AddSingleton<WorkerDisconnectNotificationService>();
builder.Services.AddHttpClient();

builder.Services.AddHostedService<WorkerRegistrationService>();
builder.Services.AddHostedService<HeartbeatService>();

var app = builder.Build();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/metrics", (WorkerState state) =>
{
    var process = System.Diagnostics.Process.GetCurrentProcess();
    var sb = new System.Text.StringBuilder();

    sb.AppendLine("# HELP simcluster_worker_busy_threads Current number of busy threads");
    sb.AppendLine("# TYPE simcluster_worker_busy_threads gauge");
    sb.AppendLine($"simcluster_worker_busy_threads{{worker_id=\"{workerId}\"}} {state.BusyThreads}");

    sb.AppendLine("# HELP simcluster_worker_free_threads Current number of free threads");
    sb.AppendLine("# TYPE simcluster_worker_free_threads gauge");
    sb.AppendLine($"simcluster_worker_free_threads{{worker_id=\"{workerId}\"}} {state.FreeThreads}");

    sb.AppendLine("# HELP simcluster_worker_max_threads Maximum number of threads");
    sb.AppendLine("# TYPE simcluster_worker_max_threads gauge");
    sb.AppendLine($"simcluster_worker_max_threads{{worker_id=\"{workerId}\"}} {state.MaxThreads}");

    sb.AppendLine("# HELP simcluster_worker_tasks_executed_total Total tasks executed successfully");
    sb.AppendLine("# TYPE simcluster_worker_tasks_executed_total counter");
    sb.AppendLine($"simcluster_worker_tasks_executed_total{{worker_id=\"{workerId}\"}} {state.TasksExecutedTotal}");

    sb.AppendLine("# HELP simcluster_worker_tasks_failed_total Total tasks failed");
    sb.AppendLine("# TYPE simcluster_worker_tasks_failed_total counter");
    sb.AppendLine($"simcluster_worker_tasks_failed_total{{worker_id=\"{workerId}\"}} {state.TasksFailedTotal}");

    sb.AppendLine("# HELP process_cpu_seconds_total Total user and system CPU time spent");
    sb.AppendLine("# TYPE process_cpu_seconds_total counter");
    sb.AppendLine($"process_cpu_seconds_total{{worker_id=\"{workerId}\"}} {process.TotalProcessorTime.TotalSeconds:F2}");

    sb.AppendLine("# HELP process_working_set_bytes Process working set in bytes");
    sb.AppendLine("# TYPE process_working_set_bytes gauge");
    sb.AppendLine($"process_working_set_bytes{{worker_id=\"{workerId}\"}} {process.WorkingSet64}");

    return Results.Text(sb.ToString(), "text/plain; version=0.0.4; charset=utf-8");
});

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Worker shutdown initiated...");

    using var scope = app.Services.CreateScope();
    var disconnectService = scope.ServiceProvider.GetRequiredService<WorkerDisconnectNotificationService>();
    disconnectService.NotifyMasterOfShutdownAsync().GetAwaiter().GetResult();

    Console.WriteLine("Worker stopped");
});

Console.WriteLine($"Worker-{workerId} starting on http://localhost:{port}");
Console.WriteLine($"Trying to connect to Master at {masterUrl}");

app.Run();