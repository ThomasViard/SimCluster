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

var app = builder.Build();

app.MapControllers();

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
