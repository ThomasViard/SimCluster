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

builder.Services.AddSingleton<WorkerState>();
builder.Services.AddScoped<IWorkerService, WorkerService>();
builder.Services.AddSingleton<WorkerDisconnectNotificationService>();
builder.Services.AddHttpClient();

builder.Services.AddHostedService<WorkerRegistrationService>();
builder.Services.AddHostedService<HeartbeatService>();

var app = builder.Build();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

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