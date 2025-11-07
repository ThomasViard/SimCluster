using System.Net.Http.Json;
using Common.Models;
using Microsoft.Extensions.Options;
using Worker.Models;

namespace Worker.Services;

public class WorkerDisconnectNotificationService
{
    private readonly WorkerConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public WorkerDisconnectNotificationService(
        IOptions<WorkerConfiguration> config,
        IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task NotifyMasterOfShutdownAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var request = new WorkerDisconnectRequest
            {
                WorkerId = _config.WorkerId,
                Reason = "graceful shutdown"
            };

            Console.WriteLine($"Notifying Master of shutdown...");

            var response = await client.PostAsJsonAsync(
                $"{_config.MasterUrl}/api/master/notifications/worker-disconnect",
                request
            );

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Master notified of shutdown");
            }
            else
            {
                Console.WriteLine($"Failed to notify Master (HTTP {response.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to notify Master: {ex.Message}");
        }
    }
}
