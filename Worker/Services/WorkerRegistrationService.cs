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
    private volatile bool _registered = false;
    private CancellationTokenSource? _cts;

    public bool IsRegistered => _registered;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => RegistrationLoop(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    private async Task RegistrationLoop(CancellationToken ct)
    {
        var attempt = 0;
        while (!ct.IsCancellationRequested && !_registered)
        {
            attempt++;
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var request = new WorkerRegistrationRequest
                {
                    WorkerId = _config.WorkerId,
                    Url = _config.WorkerUrl
                };
                var response = await client.PostAsJsonAsync($"{_config.MasterUrl}/api/master/register", request, ct);
                if (response.IsSuccessStatusCode)
                {
                    _registered = true;
                    Console.WriteLine($"Registered with Master (attempt {attempt})");
                    break;
                }
                else
                {
                    Console.WriteLine($"Registration failed (attempt {attempt}): HTTP {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Registration error (attempt {attempt}): {ex.Message}");
            }

            var delaySeconds = Math.Min(30, attempt switch
            {
                <= 3 => 2,
                <= 6 => 5,
                _ => 10
            });
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct).ContinueWith(_ => { });
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    public async Task<bool> TryRegisterOnceAsync()
    {
        if (_registered) return true;
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var request = new WorkerRegistrationRequest
            {
                WorkerId = _config.WorkerId,
                Url = _config.WorkerUrl
            };
            var response = await client.PostAsJsonAsync($"{_config.MasterUrl}/api/master/register", request);
            if (response.IsSuccessStatusCode)
            {
                _registered = true;
                Console.WriteLine("Registered with Master (opportunistic)");
                return true;
            }
        }
        catch { }
        return false;
    }
}
