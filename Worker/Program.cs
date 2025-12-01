using Common;
using Worker.Models;
using Worker.Services;

var workerId = Environment.GetEnvironmentVariable("WORKER_ID") ?? "1";
var port = 5000 + int.Parse(workerId);
var masterUrl = Environment.GetEnvironmentVariable("MASTER_URL") ?? "http://localhost:8080";

Console.SetOut(new PrefixedConsoleWriter(Console.Out, $"[Worker-{workerId}] "));

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.None);

// Controllers
builder.Services.AddControllers();

// Configuration
builder.Services.Configure<WorkerConfiguration>(options =>
{
    options.WorkerId = workerId;
    options.WorkerUrl = $"http://localhost:{port}";
    options.MasterUrl = masterUrl;
});

// Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});

// Services
builder.Services.AddSingleton<WorkerState>();
builder.Services.AddScoped<IWorkerService, WorkerService>();
builder.Services.AddSingleton<WorkerDisconnectNotificationService>();
builder.Services.AddHttpClient();

// Background Services
builder.Services.AddHostedService<WorkerRegistrationService>();
builder.Services.AddHostedService<HeartbeatService>();

var app = builder.Build();

// Map Controllers
app.MapControllers();

// Gérer l'arrêt propre du Worker
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