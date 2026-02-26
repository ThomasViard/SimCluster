﻿using Common;
using Master.Services;

Console.SetOut(new PrefixedConsoleWriter(Console.Out, "[Master] "));

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.None);

builder.Services.AddControllers();

builder.Services.AddSingleton<WorkerRegistry>();
builder.Services.AddScoped<IWorkerManagementService, WorkerManagementService>();
builder.Services.AddSingleton<ShutdownNotificationService>();
builder.Services.AddSingleton<TaskQueue>();

var schedulerType = Environment.GetEnvironmentVariable("SCHEDULER") ?? "round-robin";
if (schedulerType == "least-loaded")
    builder.Services.AddSingleton<IScheduler, LeastLoadedScheduler>();
else
    builder.Services.AddSingleton<IScheduler, RoundRobinScheduler>();

builder.Services.AddScoped<ITaskManagementService, TaskManagementService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<DockerScalingService>();

builder.Services.AddHostedService<WorkerMonitoringService>();
builder.Services.AddHostedService<TaskDispatcherService>();

var app = builder.Build();

app.MapControllers();

app.MapGet("/metrics", (TaskQueue taskQueue, WorkerRegistry registry) =>
{
    var sb = new System.Text.StringBuilder();

    sb.AppendLine("# HELP simcluster_tasks_pending Number of pending tasks");
    sb.AppendLine("# TYPE simcluster_tasks_pending gauge");
    sb.AppendLine($"simcluster_tasks_pending {taskQueue.PendingCount}");

    sb.AppendLine("# HELP simcluster_tasks_running Number of running tasks");
    sb.AppendLine("# TYPE simcluster_tasks_running gauge");
    sb.AppendLine($"simcluster_tasks_running {taskQueue.RunningCount}");

    sb.AppendLine("# HELP simcluster_tasks_completed_total Total completed tasks");
    sb.AppendLine("# TYPE simcluster_tasks_completed_total counter");
    sb.AppendLine($"simcluster_tasks_completed_total {taskQueue.CompletedCount}");

    sb.AppendLine("# HELP simcluster_tasks_failed_total Total failed tasks");
    sb.AppendLine("# TYPE simcluster_tasks_failed_total counter");
    sb.AppendLine($"simcluster_tasks_failed_total {taskQueue.FailedCount}");

    sb.AppendLine("# HELP simcluster_workers_total Total registered workers");
    sb.AppendLine("# TYPE simcluster_workers_total gauge");
    sb.AppendLine($"simcluster_workers_total {registry.GetWorkerCount()}");

    sb.AppendLine("# HELP simcluster_workers_available Number of available workers");
    sb.AppendLine("# TYPE simcluster_workers_available gauge");
    sb.AppendLine($"simcluster_workers_available {registry.GetAvailableWorkerCount()}");

    return Results.Text(sb.ToString(), "text/plain; version=0.0.4; charset=utf-8");
});

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Master shutdown initiated...");

    using var scope = app.Services.CreateScope();
    var shutdownService = scope.ServiceProvider.GetRequiredService<ShutdownNotificationService>();
    shutdownService.NotifyAllWorkersAsync().GetAwaiter().GetResult();

    Console.WriteLine("Master stopped");
});

var url = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:8080";
Console.WriteLine($"Starting Master on {url}");

app.Run();
