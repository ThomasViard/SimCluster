using System.Net.Http.Json;
using Common.Models;
using Microsoft.Extensions.Options;
using Worker.Models;

namespace Worker.Services;

public class WorkerRegistrationService(
    IOptions<WorkerConfiguration> config,
    IHttpClientFactory httpClientFactory) : IHostedService
{
    private readonly WorkerConfiguration _config = config.Value;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(1000, cancellationToken);

        try
        {
            var client = _httpClientFactory.CreateClient();
            var request = new WorkerRegistrationRequest
            {
                WorkerId = _config.WorkerId,
                Url = _config.WorkerUrl
            };

            var response = await client.PostAsJsonAsync($"{_config.MasterUrl}/api/master/register", request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Registered with Master");
            }
            else
            {
                Console.WriteLine($"Registration failed: HTTP {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register with Master: {ex.Message}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Unregistering from Master...");
        return Task.CompletedTask;
    }
}
