using Common;
using Master.Services;

Console.SetOut(new PrefixedConsoleWriter(Console.Out, "[Master] "));

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.None);

builder.Services.AddControllers();

// Services
builder.Services.AddSingleton<WorkerRegistry>();
builder.Services.AddScoped<IWorkerManagementService, WorkerManagementService>();
builder.Services.AddSingleton<ShutdownNotificationService>();
builder.Services.AddHttpClient();

// Background Services
builder.Services.AddHostedService<WorkerMonitoringService>();

var app = builder.Build();

app.MapControllers();

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Master shutdown initiated...");

    // Notifier tous les Workers de l'arrêt
    using var scope = app.Services.CreateScope();
    var shutdownService = scope.ServiceProvider.GetRequiredService<ShutdownNotificationService>();
    shutdownService.NotifyAllWorkersAsync().GetAwaiter().GetResult();

    Console.WriteLine("Master stopped");
});

Console.WriteLine("Starting Master on http://localhost:8080");

app.Run("http://localhost:8080");
