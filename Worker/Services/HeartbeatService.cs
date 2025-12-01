using System.Net.Http.Json;
using Common.Models;
using Microsoft.Extensions.Options;
using Worker.Models;

namespace Worker.Services;

public class HeartbeatService(
    IOptions<WorkerConfiguration> config,
    WorkerState state,
    IHttpClientFactory httpClientFactory) : IHostedService, IDisposable
{
    private readonly WorkerConfiguration _config = config.Value;
    private readonly WorkerState _state = state;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private Timer? _timer;

    private bool _isConnectedToMaster = false;
    private int _consecutiveFailures = 0;
    private const int MaxFailuresBeforeDisconnect = 3;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Heartbeat service started");
        _timer = new Timer(SendHeartbeatAsync, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        return Task.CompletedTask;
    }

    private async void SendHeartbeatAsync(object? state)
    {
        try
        {
            var success = await SendHeartbeat();

            if (!success)
            {
                _consecutiveFailures++;

                if (_isConnectedToMaster && _consecutiveFailures >= MaxFailuresBeforeDisconnect)
                {
                    Console.WriteLine("Lost connection to Master");
                    Console.WriteLine($"Unable to reach Master at {_config.MasterUrl}");
                    Console.WriteLine("Worker is still running but isolated from cluster");
                    _isConnectedToMaster = false;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Heartbeat error: {ex.Message}");
        }
    }

    private async Task<bool> SendHeartbeat()
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var heartbeat = new WorkerHeartbeatRequest
            {
                WorkerId = _config.WorkerId,
                IsReady = _state.IsReady,
                FreeThreads = _state.FreeThreads,
                Timestamp = DateTime.UtcNow
            };

            var response = await client.PostAsJsonAsync($"{_config.MasterUrl}/api/master/heartbeat", heartbeat);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Heartbeat sent");
                return true;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine("Master does not recognize this worker yet (404). Attempting registration...");
                try
                {
                    var regClient = _httpClientFactory.CreateClient();
                    regClient.Timeout = TimeSpan.FromSeconds(5);
                    var regRequest = new WorkerRegistrationRequest
                    {
                        WorkerId = _config.WorkerId,
                        Url = _config.WorkerUrl
                    };
                    var regResponse = await regClient.PostAsJsonAsync($"{_config.MasterUrl}/api/master/register", regRequest);
                    if (regResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Registration succeeded after heartbeat 404");
                    }
                    else
                    {
                        Console.WriteLine($"Opportunistic registration failed: HTTP {regResponse.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Opportunistic registration error: {ex.Message}");
                }
                return false;
            }
            else
            {
                Console.WriteLine($"Heartbeat failed: HTTP {response.StatusCode}");
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Heartbeat failed: {ex.Message}");
            return false;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Heartbeat timeout");
            return false;
        }
    }

    public bool IsConnectedToMaster => _isConnectedToMaster;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Stopping heartbeat service...");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
